"""Classify native Android UI evidence without relying on dumpsys subsection fields."""
import xml.etree.ElementTree as ET

def anr_messages(xml):
    texts=[node.get('text','').replace('\u2019', "'") for node in ET.fromstring(xml).iter('node')]
    return [text for text in texts if "isn't responding" in text or 'is not responding' in text]

def has_game_focus(xml, package):
    if anr_messages(xml): return False
    nodes=list(ET.fromstring(xml).iter('node'))
    if not nodes or nodes[0].get('package')!=package: return False
    focused={node.get('package') for node in nodes if node.get('focused')=='true'}
    return focused=={package}
