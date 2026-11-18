"""Fresh clones import textures before editor plugins preload their SVGs."""
from pathlib import Path
import os, re, signal, subprocess, sys

ROOT = Path(__file__).resolve().parents[1]
ANSI = re.compile(r'\x1b\[[0-?]*[ -/]*[@-~]')


def post_import_native_crash(result):
    """Godot 4.6.1 can abort while disposing the initialized .NET editor.

    Only retry a native signal after the final editor-layout completion marker.
    Script/compiler failures and crashes earlier in importing remain failures.
    """
    output = ANSI.sub('', result.stdout or '')
    return (result.returncode in (-signal.SIGABRT, -signal.SIGSEGV, 134, 139)
            and re.search(r'\[\s*DONE\s*\]\s+loading_editor_layout', output) is not None
            and 'SCRIPT ERROR:' not in output and 'error CS' not in output)


def editor_import(root, stage, runner=subprocess.run):
    command = [os.environ.get('GODOT', 'godot'), '--headless', '--editor', '--path', str(root), '--import']
    folder = root / 'build'
    folder.mkdir(exist_ok=True)
    for attempt in range(1, 4):
        result = runner(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        output = result.stdout or ''
        (folder / f'verification-import-{stage}-{attempt}.log').write_text(output)
        sys.stdout.write(output)
        sys.stdout.flush()
        if result.returncode == 0:
            return
        if attempt == 3 or not post_import_native_crash(result):
            result.check_returncode()
        print(f'Retrying post-import native Godot editor crash ({stage}, attempt {attempt}/3); original log retained.', flush=True)


def main(root=ROOT):
    project = root / 'project.godot'
    original = project.read_text()
    # Cancellation must unwind the temporary plugin configuration too.
    signal.signal(signal.SIGTERM, lambda *_: sys.exit(1))
    try:
        lines = ['enabled=PackedStringArray()' if line.startswith('enabled=PackedStringArray(') else line
                 for line in original.splitlines()]
        project.write_text('\n'.join(lines) + '\n')
        editor_import(root, 'without-plugins')
    finally:
        project.write_text(original)
    editor_import(root, 'with-plugins')


if __name__ == '__main__':
    main()
