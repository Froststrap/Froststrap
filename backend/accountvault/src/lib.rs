// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

//! Password-protected blobs for account exports.
//!
//! Layout: magic, version, Argon2id m/t/p (u32 LE), salt, nonce, AES-256-GCM ciphertext.
//! Everything before the ciphertext is authenticated as associated data.

use aes_gcm::aead::{Aead, KeyInit, Payload};
use aes_gcm::{Aes256Gcm, Nonce};
use argon2::{Algorithm, Argon2, Params, Version};
use std::{ptr, slice};
use zeroize::{Zeroize, Zeroizing};

const MAGIC: &[u8; 4] = b"FSAV";
const VERSION: u8 = 1;
const SALT_LEN: usize = 16;
const NONCE_LEN: usize = 12;
const HEADER_LEN: usize = 4 + 1 + 4 * 3 + SALT_LEN + NONCE_LEN;

// 64 MiB, 3 passes
const M_COST: u32 = 64 * 1024;
const T_COST: u32 = 3;
const P_COST: u32 = 1;

// a crafted file shouldn't be able to make us allocate gigabytes or spin for minutes
const MAX_M_COST: u32 = 1024 * 1024;
const MAX_T_COST: u32 = 16;
const MAX_P_COST: u32 = 16;

pub const OK: i32 = 0;
pub const ERR_INVALID_ARG: i32 = -1;
pub const ERR_WRONG_PASSWORD: i32 = -2;
pub const ERR_NOT_A_VAULT: i32 = -3;
pub const ERR_UNSUPPORTED_VERSION: i32 = -4;
pub const ERR_INTERNAL: i32 = -5;

#[derive(Debug, PartialEq, Eq)]
pub enum VaultError {
    WrongPassword,
    NotAVault,
    UnsupportedVersion,
    Internal,
}

impl VaultError {
    fn code(&self) -> i32 {
        match self {
            Self::WrongPassword => ERR_WRONG_PASSWORD,
            Self::NotAVault => ERR_NOT_A_VAULT,
            Self::UnsupportedVersion => ERR_UNSUPPORTED_VERSION,
            Self::Internal => ERR_INTERNAL,
        }
    }
}

fn derive_key(password: &[u8], salt: &[u8], m: u32, t: u32, p: u32) -> Result<Zeroizing<[u8; 32]>, VaultError> {
    let params = Params::new(m, t, p, Some(32)).map_err(|_| VaultError::NotAVault)?;
    let mut key = Zeroizing::new([0u8; 32]);
    Argon2::new(Algorithm::Argon2id, Version::V0x13, params)
        .hash_password_into(password, salt, key.as_mut())
        .map_err(|_| VaultError::Internal)?;
    Ok(key)
}

pub fn seal(password: &[u8], plain: &[u8]) -> Result<Vec<u8>, VaultError> {
    seal_with(password, plain, M_COST, T_COST, P_COST)
}

fn seal_with(password: &[u8], plain: &[u8], m: u32, t: u32, p: u32) -> Result<Vec<u8>, VaultError> {
    let mut salt = [0u8; SALT_LEN];
    let mut nonce = [0u8; NONCE_LEN];
    getrandom::fill(&mut salt).map_err(|_| VaultError::Internal)?;
    getrandom::fill(&mut nonce).map_err(|_| VaultError::Internal)?;

    let mut out = Vec::with_capacity(HEADER_LEN + plain.len() + 16);
    out.extend_from_slice(MAGIC);
    out.push(VERSION);
    out.extend_from_slice(&m.to_le_bytes());
    out.extend_from_slice(&t.to_le_bytes());
    out.extend_from_slice(&p.to_le_bytes());
    out.extend_from_slice(&salt);
    out.extend_from_slice(&nonce);

    let key = derive_key(password, &salt, m, t, p)?;
    let cipher = Aes256Gcm::new_from_slice(key.as_ref()).map_err(|_| VaultError::Internal)?;
    let sealed = cipher
        .encrypt(Nonce::from_slice(&nonce), Payload { msg: plain, aad: &out })
        .map_err(|_| VaultError::Internal)?;

    out.extend_from_slice(&sealed);
    Ok(out)
}

pub fn open(password: &[u8], data: &[u8]) -> Result<Zeroizing<Vec<u8>>, VaultError> {
    if data.len() < 5 || &data[..4] != MAGIC {
        return Err(VaultError::NotAVault);
    }
    if data[4] != VERSION {
        return Err(VaultError::UnsupportedVersion);
    }
    if data.len() < HEADER_LEN + 16 {
        return Err(VaultError::NotAVault);
    }

    let u32_at = |at: usize| u32::from_le_bytes(data[at..at + 4].try_into().unwrap());
    let (m, t, p) = (u32_at(5), u32_at(9), u32_at(13));
    if m > MAX_M_COST || t > MAX_T_COST || p > MAX_P_COST {
        return Err(VaultError::NotAVault);
    }

    let (header, sealed) = data.split_at(HEADER_LEN);
    let salt = &header[17..17 + SALT_LEN];
    let nonce = &header[17 + SALT_LEN..];

    let key = derive_key(password, salt, m, t, p)?;
    let cipher = Aes256Gcm::new_from_slice(key.as_ref()).map_err(|_| VaultError::Internal)?;
    cipher
        .decrypt(Nonce::from_slice(nonce), Payload { msg: sealed, aad: header })
        .map(Zeroizing::new)
        // a wrong password and a tampered file look the same to GCM
        .map_err(|_| VaultError::WrongPassword)
}

unsafe fn bytes<'a>(data: *const u8, len: usize) -> Option<&'a [u8]> {
    if data.is_null() {
        return (len == 0).then_some(&[]);
    }
    Some(unsafe { slice::from_raw_parts(data, len) })
}

unsafe fn hand_out(mut buf: Vec<u8>, out: *mut *mut u8, out_len: *mut usize) {
    buf.shrink_to_fit();
    let boxed = buf.into_boxed_slice();
    unsafe {
        *out_len = boxed.len();
        *out = Box::into_raw(boxed).cast();
    }
}

/// Encrypts `plain` with `password`. On success `out`/`out_len` hold a buffer for `vault_free`.
///
/// # Safety
/// The input pointers must be valid for their lengths (or null with length 0);
/// `out` and `out_len` must be writable.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vault_seal(
    password: *const u8,
    password_len: usize,
    plain: *const u8,
    plain_len: usize,
    out: *mut *mut u8,
    out_len: *mut usize,
) -> i32 {
    let (Some(password), Some(plain)) = (unsafe { bytes(password, password_len) }, unsafe { bytes(plain, plain_len) }) else {
        return ERR_INVALID_ARG;
    };
    if password.is_empty() || out.is_null() || out_len.is_null() {
        return ERR_INVALID_ARG;
    }

    match seal(password, plain) {
        Ok(sealed) => {
            unsafe { hand_out(sealed, out, out_len) };
            OK
        }
        Err(e) => e.code(),
    }
}

/// Decrypts a `vault_seal` buffer. On success `out`/`out_len` hold a buffer for `vault_free`.
///
/// # Safety
/// Same as `vault_seal`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vault_open(
    password: *const u8,
    password_len: usize,
    data: *const u8,
    data_len: usize,
    out: *mut *mut u8,
    out_len: *mut usize,
) -> i32 {
    let (Some(password), Some(data)) = (unsafe { bytes(password, password_len) }, unsafe { bytes(data, data_len) }) else {
        return ERR_INVALID_ARG;
    };
    if out.is_null() || out_len.is_null() {
        return ERR_INVALID_ARG;
    }

    match open(password, data) {
        Ok(mut plain) => {
            unsafe { hand_out(std::mem::take(&mut *plain), out, out_len) };
            OK
        }
        Err(e) => e.code(),
    }
}

/// Wipes and frees a buffer from `vault_seal` or `vault_open`.
///
/// # Safety
/// `data`/`len` must come from one of those calls and not be freed yet.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vault_free(data: *mut u8, len: usize) {
    if data.is_null() {
        return;
    }
    let mut boxed = unsafe { Box::from_raw(ptr::slice_from_raw_parts_mut(data, len)) };
    boxed.zeroize();
}

#[cfg(test)]
mod tests {
    use super::*;

    // cheap params so the tests stay fast
    fn quick(password: &[u8], plain: &[u8]) -> Vec<u8> {
        seal_with(password, plain, 8, 1, 1).unwrap()
    }

    #[test]
    fn round_trip() {
        let sealed = quick(b"correct horse", b"{\"accounts\":[]}");
        assert_eq!(open(b"correct horse", &sealed).unwrap().as_slice(), b"{\"accounts\":[]}");
    }

    #[test]
    fn default_params_round_trip() {
        let sealed = seal(b"pw", b"hi").unwrap();
        assert_eq!(&sealed[5..17], [M_COST.to_le_bytes(), T_COST.to_le_bytes(), P_COST.to_le_bytes()].concat());
        assert_eq!(open(b"pw", &sealed).unwrap().as_slice(), b"hi");
    }

    #[test]
    fn wrong_password() {
        let sealed = quick(b"right", b"secret");
        assert_eq!(open(b"wrong", &sealed).unwrap_err(), VaultError::WrongPassword);
    }

    #[test]
    fn tampered_header_or_body() {
        let sealed = quick(b"pw", b"secret");
        for i in [20, HEADER_LEN - 1, HEADER_LEN, sealed.len() - 1] {
            let mut bad = sealed.clone();
            bad[i] ^= 1;
            assert_eq!(open(b"pw", &bad).unwrap_err(), VaultError::WrongPassword, "byte {i}");
        }
    }

    #[test]
    fn rejects_other_files() {
        assert_eq!(open(b"pw", b"").unwrap_err(), VaultError::NotAVault);
        assert_eq!(open(b"pw", b"{\"accounts\":[]}").unwrap_err(), VaultError::NotAVault);
        assert_eq!(open(b"pw", &quick(b"pw", b"x")[..HEADER_LEN]).unwrap_err(), VaultError::NotAVault);

        let mut future = quick(b"pw", b"x");
        future[4] = 2;
        assert_eq!(open(b"pw", &future).unwrap_err(), VaultError::UnsupportedVersion);
    }

    #[test]
    fn rejects_huge_params() {
        let mut bad = quick(b"pw", b"x");
        bad[5..9].copy_from_slice(&u32::MAX.to_le_bytes());
        assert_eq!(open(b"pw", &bad).unwrap_err(), VaultError::NotAVault);
    }

    #[test]
    fn fresh_salt_and_nonce() {
        assert_ne!(quick(b"pw", b"same"), quick(b"pw", b"same"));
    }

    #[test]
    fn c_abi_round_trip() {
        let (pw, msg) = (b"pw", b"hello");
        let (mut sealed, mut sealed_len) = (ptr::null_mut(), 0);
        let (mut plain, mut plain_len) = (ptr::null_mut(), 0);
        unsafe {
            assert_eq!(vault_seal(pw.as_ptr(), pw.len(), msg.as_ptr(), msg.len(), &mut sealed, &mut sealed_len), OK);
            assert_eq!(vault_open(pw.as_ptr(), pw.len(), sealed, sealed_len, &mut plain, &mut plain_len), OK);
            assert_eq!(slice::from_raw_parts(plain, plain_len), msg);
            assert_eq!(vault_open(b"x".as_ptr(), 1, sealed, sealed_len, &mut plain, &mut plain_len), ERR_WRONG_PASSWORD);
            assert_eq!(vault_seal(ptr::null(), 0, msg.as_ptr(), msg.len(), &mut sealed, &mut sealed_len), ERR_INVALID_ARG);
            vault_free(sealed, sealed_len);
            vault_free(plain, plain_len);
        }
    }
}
