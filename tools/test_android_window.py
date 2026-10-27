"""Regression cases for the Android 35 focus-check failure and blocking system dialogs."""
import unittest
from android_window import anr_messages, has_game_focus

PACKAGE='com.cigthepig.mosslight.dev'
class NativeWindowChecks(unittest.TestCase):
    def test_godot_focused_view_without_dumpsys_field(self):
        # Structure observed in the failed hosted run's native-window.xml.
        xml=f'<hierarchy><node package="{PACKAGE}" focused="false"><node package="{PACKAGE}" class="android.view.View" focused="true"/></node></hierarchy>'
        self.assertTrue(has_game_focus(xml,PACKAGE))
    def test_launcher_anr_over_background_game_is_not_focus(self):
        xml=f'<hierarchy><node package="android" focused="true" text="Pixel Launcher isn’t responding"><node package="{PACKAGE}" focused="true"/></node></hierarchy>'
        self.assertFalse(has_game_focus(xml,PACKAGE))
        self.assertEqual(anr_messages(xml),["Pixel Launcher isn't responding"])
    def test_game_anr_is_never_accepted(self):
        xml=f'<hierarchy><node package="{PACKAGE}" focused="true" text="Mosslight is not responding"/></hierarchy>'
        self.assertFalse(has_game_focus(xml,PACKAGE))
    def test_package_without_focused_native_view_is_not_ready(self):
        self.assertFalse(has_game_focus(f'<hierarchy><node package="{PACKAGE}" focused="false"/></hierarchy>',PACKAGE))

if __name__=='__main__': unittest.main()
