"""Create a flat, checked install ZIP; large raw APK handoffs can be truncated in transit.

Usage: python3 tools/package-install.py path/to/Mosslight.apk [path/to/install.zip]
After saving the ZIP for delivery, verify that downloaded copy against archive_sha256
and the APK inside against apk_sha256. A successful upload alone is insufficient.
"""
from pathlib import Path
import hashlib, json, sys, zipfile

apk = Path(sys.argv[1]).resolve()
archive = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else apk.with_suffix('.install.zip')
if apk == archive or not apk.is_file():
    raise SystemExit('Expected a source APK and a different output ZIP path')
with zipfile.ZipFile(apk) as source:
    if source.testzip() is not None or 'AndroidManifest.xml' not in source.namelist():
        raise SystemExit('APK is incomplete or corrupt')
apk_hash = hashlib.sha256(apk.read_bytes()).hexdigest()
archive.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as package:
    package.write(apk, apk.name)
with zipfile.ZipFile(archive) as package:
    if package.testzip() is not None or hashlib.sha256(package.read(apk.name)).hexdigest() != apk_hash:
        raise SystemExit('Install ZIP failed its round-trip check')
print(json.dumps(dict(archive_path=str(archive), archive_bytes=archive.stat().st_size,
    archive_sha256=hashlib.sha256(archive.read_bytes()).hexdigest(), apk_name=apk.name,
    apk_bytes=apk.stat().st_size, apk_sha256=apk_hash)))
