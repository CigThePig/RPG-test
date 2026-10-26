"""Fail closed on a signed package missing C# gameplay or runtime. No runtime claims."""
from pathlib import Path
import datetime, hashlib, json, os, re, struct, subprocess, sys, zipfile
apk = Path(sys.argv[1]); out = apk.parent
assert apk.is_file() and apk.stat().st_size > 1_000_000, 'Missing or empty APK'
def run(*args):
    return subprocess.check_output(args, stderr=subprocess.STDOUT, text=True)
badging = run('aapt', 'dump', 'badging', str(apk))
assert "package: name='com.cigthepig.mosslight.dev'" in badging, badging
assert "launchable-activity:" in badging, 'No launcher activity'
assert "native-code: 'arm64-v8a'" in badging, 'Not ARM64-only'
signature = run('apksigner', 'verify', '--verbose', '--print-certs', str(apk))
assert 'certificate SHA-256 digest:' in signature, 'No signing certificate'
manifest = run('aapt', 'dump', 'xmltree', str(apk), 'AndroidManifest.xml')
(out/'package-manifest.log').write_text(manifest)
(out/'package-signature.log').write_text(signature)
# Godot APK exports resources as ZIP assets, including embedded .NET publish files.
with zipfile.ZipFile(apk) as z:
    names = z.namelist()
    (out/'package-files.log').write_text('\n'.join(names))
    def require(suffix):
        hits = [n for n in names if n.endswith(suffix)]
        assert hits, f'Missing packaged {suffix}'
        assert all(z.getinfo(n).file_size > 0 for n in hits), f'Empty {suffix}'
        return hits
    for suffix in ['RpgTest.dll', 'GodotSharp.dll', 'System.Private.CoreLib.dll', '.dotnet-publish-manifest',
                   'game/data/items.json', 'game/data/quests.json', 'game/data/dungeon_rules.json',
                   'game/data/warden.dialogue', 'game/dev/Foundation.tscn']:
        require(suffix)
    require('libgodot_android.so')
    assert any('mono' in n.lower() and n.endswith('.so') for n in names), 'Missing Mono runtime'
    native = [n for n in names if n.startswith('lib/') and n.endswith('.so')]
    for n in native:
        assert n.startswith('lib/arm64-v8a/'), f'Unexpected native architecture: {n}'
        data = z.read(n)
        assert data[:4] == b'\x7fELF' and data[4] == 2 and struct.unpack_from('<H',data,18)[0] == 183, f'Invalid AArch64 ELF: {n}'
checksum = hashlib.sha256(apk.read_bytes()).hexdigest()
(apk.with_suffix('.apk.sha256')).write_text(f'{checksum}  {apk.name}\n')
metadata = dict(commit=run('git','rev-parse','HEAD').strip(), engine='4.6.1 .NET',
                dotnet=run('dotnet','--version').strip(), framework='net9.0', android='Official Godot 4.6.1 template / ARM64; see badging for SDK levels',
                package='com.cigthepig.mosslight.dev', filename=apk.name, bytes=apk.stat().st_size,
                sha256=checksum, timestamp=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                signing=os.environ.get('SIGNING_MODE','unknown'),
                verification='desktop integration and visual checks; APK metadata/signature/resources/runtime/ELF passed',
                android_runtime='Installation and launch NOT exercised by package verification',
                badging=badging, signature=signature)
(out/'build-metadata.json').write_text(json.dumps(metadata, indent=2))
print(json.dumps({k:v for k,v in metadata.items() if k not in ['badging','signature']}, indent=2))
if os.environ.get('GITHUB_STEP_SUMMARY'):
    with open(os.environ['GITHUB_STEP_SUMMARY'],'a') as f:
        f.write(f'## Mosslight ARM64 development APK\n\n`{apk.name}` · {apk.stat().st_size/1048576:.1f} MiB\n\n'
                f'Source: `{metadata["commit"]}`\n\nSHA-256: `{checksum}`\n\n'
                f'Signing: {metadata["signing"]}\n\nStatic package checks passed. Android installation/launch requires a device check.\n')
