"""Regression for Android's observed 1024-byte Godot log truncation on wide fractional geometry."""
import json
import unittest
from android_layout import latest_display, control_rectangles, LogHistory


class AndroidLayoutChecks(unittest.TestCase):
    def display(self):
        return {'physical': '(1560, 720)', 'drawable': '(1560, 720)', 'logical': '(1733, 800)',
                'safe': '(0, 0), (1732.9998, 799.99994)',
                'gameplay': '(226.49994, 0), (1279.9999, 799.99994)',
                'scale': .9001731, 'scaleY': .9, 'originX': 0, 'originY': 0,
                'stickWidth': 180.03462, 'zoom': '(1.5, 1.5)', 'worldSpan': '(1155.3333, 533.3333)'}

    def rows(self):
        return [dict(id=name, x=1215.6836, y=559.7999, width=108.02075, height=118.79999)
                for name in ('attack', 'dodge', 'interact', 'ember', 'mend', 'potion', 'inventory', 'menu', 'stick', 'hud')]

    def test_fractional_wide_records_fit_observed_log_limit(self):
        display = 'MOSSLIGHT_DISPLAY ' + json.dumps(self.display(), separators=(',', ':'))
        rows = ['MOSSLIGHT_CONTROL_RECT '+json.dumps(row, separators=(',', ':')) for row in self.rows()]
        for line in [display]+rows:
            self.assertLess(len(line.encode()), 900)
        logs = '\n'.join('I godot: '+line for line in [display]+rows)
        self.assertEqual(latest_display(logs)['logical'], '(1733, 800)')
        self.assertEqual(len(control_rectangles(logs)), 10)

    def test_truncated_latest_display_is_not_hidden_by_an_older_record(self):
        valid = 'MOSSLIGHT_DISPLAY '+json.dumps(self.display())
        # Former parser greedily matched an inner closing brace in a truncated object.
        truncated = 'MOSSLIGHT_DISPLAY {"controlRects":{"attack":{"x":1215.6836}}'
        with self.assertRaisesRegex(AssertionError, 'truncated'):
            latest_display(valid+'\n'+truncated)

    def test_latest_control_target_wins(self):
        rows = self.rows(); rows.append(dict(rows[0], x=100))
        logs = '\n'.join('MOSSLIGHT_CONTROL_RECT '+json.dumps(r) for r in rows)
        self.assertEqual(control_rectangles(logs)['attack']['x'], 100)

    def test_missing_control_is_not_accepted(self):
        logs = '\n'.join('MOSSLIGHT_CONTROL_RECT '+json.dumps(r) for r in self.rows()[:-1])
        with self.assertRaisesRegex(AssertionError, 'Missing physical'):
            control_rectangles(logs)


class AndroidLogHistoryChecks(unittest.TestCase):
    def test_rollover_retains_actual_target_and_display(self):
        history=LogHistory()
        display='01-01 00:00:00 I godot: MOSSLIGHT_DISPLAY '+json.dumps(AndroidLayoutChecks().display())
        target='01-01 00:00:00 I godot: MOSSLIGHT_CONTROL version=3 id=dodge x=800 y=500'
        history.merge(display+'\n'+target)
        logs=history.merge('01-01 00:00:01 I godot: MOSSLIGHT_RUNTIME tick=60')
        self.assertIn(target,logs)
        self.assertEqual(latest_display(logs)['logical'],'(1733, 800)')

    def test_repeated_snapshots_do_not_double_count_input(self):
        history=LogHistory();first='01-01 00:00:00 I godot: MOSSLIGHT_COMBAT kind=attack_started tick=1'
        second='01-01 00:00:01 I godot: MOSSLIGHT_COMBAT kind=attack_started tick=50'
        history.merge(first);history.merge(first)
        logs=history.merge(first+'\n'+second)
        self.assertEqual(logs.count('kind=attack_started'),2)

    def test_genuine_duplicate_events_are_not_hidden(self):
        history=LogHistory();line='01-01 00:00:00 I godot: MOSSLIGHT_COMBAT kind=attack_started tick=1'
        logs=history.merge(line+'\n'+line)
        self.assertEqual(logs.count('kind=attack_started'),2)
        self.assertEqual(history.merge(line+'\n'+line).count('kind=attack_started'),2)
        self.assertEqual(history.merge(line).count('kind=attack_started'),2)

    def test_new_real_target_stays_last_after_rollover(self):
        history=LogHistory()
        old='01-01 00:00:00 I godot: MOSSLIGHT_CONTROL version=3 id=dodge x=800 y=500'
        new='01-01 00:00:01 I godot: MOSSLIGHT_CONTROL version=3 id=dodge x=900 y=550'
        history.merge(old);history.merge(new)
        logs=history.merge(new)
        self.assertTrue(logs.endswith(new))
        self.assertEqual(logs.count('id=dodge'),2)

    def test_earlier_errors_are_not_hidden_and_missing_records_are_not_invented(self):
        history=LogHistory();error='01-01 00:00:00 E godot   : ERROR: failed shader'
        history.merge(error)
        logs=history.merge('01-01 00:00:01 I system: unrelated')
        self.assertIn(error,logs)
        with self.assertRaises(AssertionError): latest_display(logs)
        with self.assertRaises(AssertionError): control_rectangles(logs)


if __name__ == '__main__':
    unittest.main()
