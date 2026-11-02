"""Ephemeral first build; optional persistent private development credentials in Actions secrets."""
from pathlib import Path
import os, subprocess
key = Path(os.environ['RUNNER_TEMP']) / 'mosslight-debug.keystore'
alias, password = 'androiddebugkey', 'android'
encoded = os.environ.get('DEV_KEYSTORE', '')
if encoded:
    import base64
    key.write_bytes(base64.b64decode(encoded, validate=True))
    alias, password = os.environ['DEV_ALIAS'], os.environ['DEV_PASSWORD']
    if not alias or not password: raise SystemExit('Persistent signing requires alias and password secrets')
    mode = 'persistent-private-development-key'
else:
    subprocess.run(['keytool', '-genkeypair', '-keystore', str(key), '-storepass', password,
                    '-keypass', password, '-alias', alias, '-keyalg', 'RSA', '-keysize', '2048',
                    '-validity', '10000', '-dname', 'CN=Mosslight Development,O=CigThePig,C=CA'], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    mode = 'ephemeral-debug-key-uninstall-required-between-runs'
key.chmod(0o600)
# Alias and password are deliberately not exported: GITHUB_ENV reaches every later
# step, including those that build and run pull-request code. Only the export steps
# receive them, directly from the workflow (falling back to this debug alias/password).
with open(os.environ['GITHUB_ENV'], 'a') as out:
    for name, value in {'GODOT_ANDROID_KEYSTORE_DEBUG_PATH':str(key),
                        'SIGNING_MODE':mode}.items():
        if '\n' in value or '\r' in value: raise SystemExit('Invalid signing environment value')
        out.write(f'{name}={value}\n')
settings = Path.home() / '.config/godot/editor_settings-4.6.tres'
settings.parent.mkdir(parents=True, exist_ok=True)
settings.write_text('[gd_resource type="EditorSettings" format=3]\n\n[resource]\n'
                    f'export/android/java_sdk_path = "{os.environ["JAVA_HOME"]}"\n'
                    f'export/android/android_sdk_path = "{os.environ["ANDROID_HOME"]}"\n')
# Version codes increase across this workflow; PR and push use identical source SHA in artifact.
preset = Path('export_presets.cfg')
preset.write_text(preset.read_text().replace('version/code=1', f'version/code={1000 + int(os.environ["GITHUB_RUN_NUMBER"])}'))
