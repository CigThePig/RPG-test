"""Small bounded Android log records; native points and logical display metadata stay distinct."""
import json


def _records(logs, marker):
    prefix = marker + ' '
    for line in logs.splitlines():
        if prefix in line:
            yield line.split(prefix, 1)[1]


def latest_display(logs):
    records = list(_records(logs, 'MOSSLIGHT_DISPLAY'))
    assert records, 'Missing actual application viewport diagnostic'
    try:
        result = json.loads(records[-1])
    except json.JSONDecodeError as error:
        raise AssertionError('Incomplete application display diagnostic; Android log record truncated') from error
    for field in ('physical', 'drawable', 'logical', 'safe', 'gameplay', 'scale', 'scaleY', 'originX', 'originY', 'stickWidth'):
        assert field in result, 'Missing application display field: ' + field
    assert result['scale'] > 0 and result['scaleY'] > 0 and result['stickWidth'] > 0
    return result


def control_rectangles(logs):
    result = {}
    for record in _records(logs, 'MOSSLIGHT_CONTROL_RECT'):
        row = json.loads(record)
        assert row['width'] > 0 and row['height'] > 0, 'Invalid physical control target'
        result[row['id']] = row
    expected = {'attack', 'dodge', 'interact', 'ember', 'mend', 'potion', 'inventory', 'menu', 'stick', 'hud'}
    assert expected <= result.keys(), 'Missing physical control rectangles: ' + str(expected-result.keys())
    return result
