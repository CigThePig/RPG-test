"""Failure contracts for importing: retry only completed native editor crashes."""
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest import TestCase, main, mock
import importlib.util, signal, subprocess

spec = importlib.util.spec_from_file_location('project_import', Path(__file__).with_name('import-project.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
COMPLETE = '\x1b[92m[ DONE ]\x1b[39m \x1b[1mloading_editor_layout\x1b[22m\n'


class ImportContracts(TestCase):
    def run_import(self, outputs):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            runner = mock.Mock(side_effect=[subprocess.CompletedProcess(['godot'], code, text) for code, text in outputs])
            with mock.patch('sys.stdout'):
                module.editor_import(root, 'test', runner)
            return runner.call_count, sorted(p.read_text() for p in (root / 'build').glob('*.log'))

    def test_native_shutdown_retry_retains_both_outputs(self):
        count, logs = self.run_import([(-signal.SIGABRT, COMPLETE+'native crash'), (0, COMPLETE+'success')])
        self.assertEqual(count, 2)
        self.assertEqual(len(logs), 2)
        self.assertTrue(any('native crash' in log for log in logs))

    def test_ordinary_import_error_fails_immediately(self):
        with self.assertRaises(subprocess.CalledProcessError):
            self.run_import([(1, COMPLETE+'resource failed')])

    def test_script_error_cannot_be_hidden_by_native_signal(self):
        result = subprocess.CompletedProcess([], -signal.SIGABRT, COMPLETE+'SCRIPT ERROR: bad script')
        self.assertFalse(module.post_import_native_crash(result))

    def test_compiler_error_cannot_be_hidden_by_native_signal(self):
        self.assertFalse(module.post_import_native_crash(subprocess.CompletedProcess([], -signal.SIGABRT, COMPLETE+'error CS1002')))

    def test_early_crash_does_not_retry(self):
        self.assertFalse(module.post_import_native_crash(subprocess.CompletedProcess([], -signal.SIGSEGV, 'Started importing')))

    def test_persistent_native_crash_has_bounded_attempts(self):
        runner = mock.Mock(return_value=subprocess.CompletedProcess([], -signal.SIGABRT, COMPLETE))
        with TemporaryDirectory() as directory, mock.patch('sys.stdout'):
            with self.assertRaises(subprocess.CalledProcessError):
                module.editor_import(Path(directory), 'test', runner)
        self.assertEqual(runner.call_count, 3)

    def test_failed_import_restores_plugin_configuration(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            original = '[editor_plugins]\nenabled=PackedStringArray("res://example/plugin.cfg")\n'
            (root / 'project.godot').write_text(original)
            with mock.patch.object(module, 'editor_import', side_effect=RuntimeError('failed')), mock.patch.object(module.signal, 'signal'):
                with self.assertRaises(RuntimeError):
                    module.main(root)
            self.assertEqual((root / 'project.godot').read_text(), original)


if __name__ == '__main__':
    main()
