"""Regression for Android's observed 1024-byte Godot log truncation on wide fractional geometry."""
import json
import unittest
from android_layout import latest_display, control_rectangles


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


if __name__ == '__main__':
    unittest.main()
