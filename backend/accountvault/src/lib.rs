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

// an export of every account is a few KB
const MAX_PLAIN_LEN: usize = 1024 * 1024;
const MAX_DATA_LEN: usize = HEADER_LEN + MAX_PLAIN_LEN + 16;

pub const OK: i32 = 0;
pub const ERR_INVALID_ARG: i32 = -1;
pub const ERR_WRONG_PASSWORD: i32 = -2;
pub const ERR_NOT_A_VAULT: i32 = -3;
pub const ERR_UNSUPPORTED_VERSION: i32 = -4;
pub const ERR_INTERNAL: i32 = -5;

#[derive(Debug, PartialEq, Eq)]
pub enum VaultError {
    InvalidArg,
    WrongPassword,
    NotAVault,
    UnsupportedVersion,
    Internal,
}

impl VaultError {
    fn code(&self) -> i32 {
        match self {
            Self::InvalidArg => ERR_INVALID_ARG,
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

// the header is read before it can be authenticated, so version 1 only accepts what seal()
// writes; anything else could make a crafted file burn memory and CPU
fn params_allowed(m: u32, t: u32, p: u32, allow_cheaper: bool) -> bool {
    if allow_cheaper {
        return (1..=M_COST).contains(&m) && (1..=T_COST).contains(&t) && p == P_COST;
    }
    (m, t, p) == (M_COST, T_COST, P_COST)
}

pub fn seal(password: &[u8], plain: &[u8]) -> Result<Vec<u8>, VaultError> {
    seal_with(password, plain, M_COST, T_COST, P_COST)
}

fn seal_with(password: &[u8], plain: &[u8], m: u32, t: u32, p: u32) -> Result<Vec<u8>, VaultError> {
    if password.is_empty() || plain.len() > MAX_PLAIN_LEN {
        return Err(VaultError::InvalidArg);
    }

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
    open_with(password, data, false)
}

fn open_with(password: &[u8], data: &[u8], allow_cheaper: bool) -> Result<Zeroizing<Vec<u8>>, VaultError> {
    if data.len() < 5 || &data[..4] != MAGIC {
        return Err(VaultError::NotAVault);
    }
    if data[4] != VERSION {
        return Err(VaultError::UnsupportedVersion);
    }
    if data.len() < HEADER_LEN + 16 || data.len() > MAX_DATA_LEN {
        return Err(VaultError::NotAVault);
    }

    let u32_at = |at: usize| u32::from_le_bytes(data[at..at + 4].try_into().unwrap());
    let (m, t, p) = (u32_at(5), u32_at(9), u32_at(13));
    if !params_allowed(m, t, p, allow_cheaper) {
        return Err(VaultError::NotAVault);
    }
    // nothing was sealed with an empty password, so skip the expensive derivation
    if password.is_empty() {
        return Err(VaultError::WrongPassword);
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
    if len > isize::MAX as usize {
        return None;
    }
    Some(unsafe { slice::from_raw_parts(data, len) })
}

// copies into an exact-size box; the source is wiped when it drops, spare capacity included
// (shrinking it in place would reallocate and leave an unwiped copy behind)
unsafe fn hand_out(buf: Zeroizing<Vec<u8>>, out: *mut *mut u8, out_len: *mut usize) {
    let boxed: Box<[u8]> = buf.as_slice().into();
    drop(buf);
    unsafe {
        *out_len = boxed.len();
        *out = Box::into_raw(boxed).cast();
    }
}

/// Clears the out params; false if either is null.
unsafe fn reset_out(out: *mut *mut u8, out_len: *mut usize) -> bool {
    if out.is_null() || out_len.is_null() {
        return false;
    }
    unsafe {
        *out = ptr::null_mut();
        *out_len = 0;
    }
    true
}

/// Encrypts `plain` with `password`. On success `out`/`out_len` hold a buffer for `vault_free`;
/// on error they're null/0, so free a previous buffer before reusing its variables.
/// Panics abort the process (release profile), they never unwind into C#.
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
    if !unsafe { reset_out(out, out_len) } {
        return ERR_INVALID_ARG;
    }
    let (Some(password), Some(plain)) = (unsafe { bytes(password, password_len) }, unsafe { bytes(plain, plain_len) }) else {
        return ERR_INVALID_ARG;
    };

    match seal(password, plain) {
        Ok(sealed) => {
            unsafe { hand_out(Zeroizing::new(sealed), out, out_len) };
            OK
        }
        Err(e) => e.code(),
    }
}

/// Decrypts a `vault_seal` buffer. Same out-param and panic rules as `vault_seal`.
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
    if !unsafe { reset_out(out, out_len) } {
        return ERR_INVALID_ARG;
    }
    let (Some(password), Some(data)) = (unsafe { bytes(password, password_len) }, unsafe { bytes(data, data_len) }) else {
        return ERR_INVALID_ARG;
    };

    match open(password, data) {
        Ok(plain) => {
            unsafe { hand_out(plain, out, out_len) };
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

    // cheap params so the tests stay fast; open_cheap accepts them, open() never does
    fn open_cheap(password: &[u8], data: &[u8]) -> Result<Zeroizing<Vec<u8>>, VaultError> {
        open_with(password, data, true)
    }

    fn quick(password: &[u8], plain: &[u8]) -> Vec<u8> {
        seal_with(password, plain, 8, 1, 1).unwrap()
    }

    #[test]
    fn round_trip() {
        let sealed = quick(b"correct horse", b"{\"accounts\":[]}");
        assert_eq!(open_cheap(b"correct horse", &sealed).unwrap().as_slice(), b"{\"accounts\":[]}");
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
        assert_eq!(open_cheap(b"wrong", &sealed).unwrap_err(), VaultError::WrongPassword);
    }

    #[test]
    fn tampered_header_or_body() {
        let sealed = quick(b"pw", b"secret");
        for i in [20, HEADER_LEN - 1, HEADER_LEN, sealed.len() - 1] {
            let mut bad = sealed.clone();
            bad[i] ^= 1;
            assert_eq!(open_cheap(b"pw", &bad).unwrap_err(), VaultError::WrongPassword, "byte {i}");
        }
    }

    #[test]
    fn rejects_other_files() {
        assert_eq!(open_cheap(b"pw", b"").unwrap_err(), VaultError::NotAVault);
        assert_eq!(open_cheap(b"pw", b"{\"accounts\":[]}").unwrap_err(), VaultError::NotAVault);
        assert_eq!(open_cheap(b"pw", &quick(b"pw", b"x")[..HEADER_LEN]).unwrap_err(), VaultError::NotAVault);

        let mut future = quick(b"pw", b"x");
        future[4] = 2;
        assert_eq!(open_cheap(b"pw", &future).unwrap_err(), VaultError::UnsupportedVersion);
    }

    fn with_params(mut data: Vec<u8>, m: u32, t: u32, p: u32) -> Vec<u8> {
        data[5..9].copy_from_slice(&m.to_le_bytes());
        data[9..13].copy_from_slice(&t.to_le_bytes());
        data[13..17].copy_from_slice(&p.to_le_bytes());
        data
    }

    #[test]
    fn rejects_other_params() {
        let base = quick(b"pw", b"x");
        for (m, t, p) in [(u32::MAX, 1, 1), (8, 64, 1), (8, 1, 16), (0, 1, 1), (8, 0, 1), (8, 1, 0), (M_COST * 2, T_COST, P_COST)] {
            let bad = with_params(base.clone(), m, t, p);
            assert_eq!(open_cheap(b"pw", &bad).unwrap_err(), VaultError::NotAVault, "{m}/{t}/{p}");
        }
    }

    #[test]
    fn production_accepts_only_default_params() {
        assert!(params_allowed(M_COST, T_COST, P_COST, false));
        assert!(!params_allowed(8, 1, 1, false));
        assert!(!params_allowed(M_COST, T_COST + 1, P_COST, false));

        assert_eq!(open(b"pw", &quick(b"pw", b"x")).unwrap_err(), VaultError::NotAVault);
        assert_eq!(open(b"pw", &seal(b"pw", b"x").unwrap()).unwrap().as_slice(), b"x");
    }

    #[test]
    fn every_truncation_is_rejected() {
        let sealed = quick(b"pw", b"secret");
        for len in 0..sealed.len() {
            let err = open_cheap(b"pw", &sealed[..len]).unwrap_err();
            assert!(matches!(err, VaultError::NotAVault | VaultError::WrongPassword), "len {len}: {err:?}");
        }
    }

    #[test]
    fn empty_plaintext_round_trip() {
        let sealed = quick(b"pw", b"");
        assert_eq!(open_cheap(b"pw", &sealed).unwrap().as_slice(), b"");
    }

    #[test]
    fn empty_password() {
        assert_eq!(seal(b"", b"x").unwrap_err(), VaultError::InvalidArg);
        assert_eq!(open_cheap(b"", &quick(b"pw", b"x")).unwrap_err(), VaultError::WrongPassword);
    }

    #[test]
    fn size_limits() {
        let max = vec![7u8; MAX_PLAIN_LEN];
        let sealed = quick(b"pw", &max);
        assert_eq!(sealed.len(), MAX_DATA_LEN);
        assert_eq!(open_cheap(b"pw", &sealed).unwrap().len(), MAX_PLAIN_LEN);

        assert_eq!(seal_with(b"pw", &vec![0u8; MAX_PLAIN_LEN + 1], 8, 1, 1).unwrap_err(), VaultError::InvalidArg);
        let mut too_big = sealed;
        too_big.push(0);
        assert_eq!(open_cheap(b"pw", &too_big).unwrap_err(), VaultError::NotAVault);
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
        // failed calls get their own outputs, since every call clears what it's given
        let (mut failed, mut failed_len) = (ptr::null_mut(), 0);
        unsafe {
            assert_eq!(vault_seal(pw.as_ptr(), pw.len(), msg.as_ptr(), msg.len(), &mut sealed, &mut sealed_len), OK);
            assert_eq!(vault_open(pw.as_ptr(), pw.len(), sealed, sealed_len, &mut plain, &mut plain_len), OK);
            assert_eq!(slice::from_raw_parts(plain, plain_len), msg);

            assert_eq!(vault_open(b"x".as_ptr(), 1, sealed, sealed_len, &mut failed, &mut failed_len), ERR_WRONG_PASSWORD);
            assert!(failed.is_null() && failed_len == 0);
            assert_eq!(vault_seal(ptr::null(), 0, msg.as_ptr(), msg.len(), &mut failed, &mut failed_len), ERR_INVALID_ARG);
            assert!(failed.is_null() && failed_len == 0);

            vault_free(sealed, sealed_len);
            vault_free(plain, plain_len);
        }
    }

    #[test]
    fn c_abi_clears_outputs_on_error() {
        let (mut out, mut out_len) = (ptr::dangling_mut::<u8>(), 99usize);
        unsafe {
            assert_eq!(vault_open(b"pw".as_ptr(), 2, b"junk".as_ptr(), 4, &mut out, &mut out_len), ERR_NOT_A_VAULT);
            assert!(out.is_null() && out_len == 0);
            assert_eq!(vault_seal(b"pw".as_ptr(), 2, ptr::null(), 5, ptr::null_mut(), &mut out_len), ERR_INVALID_ARG);
        }
    }

    #[test]
    fn c_abi_repeated_cycles_and_empty_plaintext() {
        let pw = b"pw";
        for msg in [&b""[..], b"a", b"{\"accounts\":[]}"] {
            for _ in 0..3 {
                let (mut sealed, mut sealed_len) = (ptr::null_mut(), 0);
                let (mut plain, mut plain_len) = (ptr::null_mut(), 0);
                unsafe {
                    // the C ABI uses the real params, so this exercises the production path too
                    assert_eq!(vault_seal(pw.as_ptr(), pw.len(), msg.as_ptr(), msg.len(), &mut sealed, &mut sealed_len), OK);
                    assert_eq!(vault_open(pw.as_ptr(), pw.len(), sealed, sealed_len, &mut plain, &mut plain_len), OK);
                    assert_eq!(slice::from_raw_parts(plain, plain_len), msg);
                    vault_free(sealed, sealed_len);
                    vault_free(plain, plain_len);
                }
            }
        }
    }
}
