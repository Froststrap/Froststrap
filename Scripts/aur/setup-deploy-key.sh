#!/usr/bin/env bash

set -euo pipefail

KEY_DIR="${HOME}/.ssh"
KEY_NAME="aur-deploy-key"

mkdir -p "${KEY_DIR}"
chmod 700 "${KEY_DIR}"

ssh-keygen \
    -t ed25519 \
    -C "github-actions-aur@froststrap" \
    -f "${KEY_DIR}/${KEY_NAME}" \
    -N ""

chmod 600 "${KEY_DIR}/${KEY_NAME}"
chmod 644 "${KEY_DIR}/${KEY_NAME}.pub"

cat <<EOF

1. Register the PUBLIC key on AUR:
     cat ${KEY_DIR}/${KEY_NAME}.pub 
     paste into https://aur.archlinux.org/account/  (SSH Public Key field)

2. Add three secrets to the GitHub repo
   (Settings -> Secrets and variables -> Actions -> New repository secret):

     Name: AUR_SSH_PRIVATE_KEY
     Value: output of:  cat ${KEY_DIR}/${KEY_NAME}

     Name: AUR_USERNAME
     Value: your AUR account name, e.g. froststrap

     Name: AUR_EMAIL
     Value: the email your AUR account uses

3. Verify the key works before pushing the workflow:
     ssh -i ${KEY_DIR}/${KEY_NAME} -o IdentitiesOnly=yes aur@aur.archlinux.org help

EOF