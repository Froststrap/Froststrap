import glob
import re
from pathlib import Path
import os
import xml.etree.ElementTree as ET

class ProjectDetector:
    def __init__(self):
        script_path = Path(os.path.dirname(os.path.abspath(__file__)))
        self.project_root = script_path.parent.parent
        self.csproj_dir = self.project_root / "Froststrap"
    
    def detect(self):
        print("Select project root:")
        print("1. Auto-detect (Froststrap.csproj)")
        print("2. Manual selection")
        
        choice = input("Enter choice (1 or 2): ").strip()
        
        if choice == "1":
            if self.csproj_dir.exists() and (self.csproj_dir / "Froststrap.csproj").exists():
                return str(self.csproj_dir)
            else:
                print("Auto-detection failed. Falling back to manual selection.")
                return self.manual_select()
        else:
            return self.manual_select()
    
    def manual_select(self):
        return input("Enter project path (the one containing Froststrap.csproj): ").strip()


def remove_unused_strings(resx_path, unused_strings):
    if not unused_strings:
        print("No unused strings to remove.")
        return False
    
    response = input(f"\nDo you want to remove {len(unused_strings)} unused strings from Strings.resx? (yes/no): ").strip().lower()
    if response not in ['yes', 'y']:
        return False
    
    try:
        tree = ET.parse(resx_path)
        root = tree.getroot()
        
        namespaces = {'': 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'}
        ET.register_namespace('', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
        
        removed_count = 0
        for data_elem in root.findall('data'):
            name_attr = data_elem.get('name')
            if name_attr in unused_strings:
                root.remove(data_elem)
                removed_count += 1
        
        tree.write(resx_path, encoding='utf-8', xml_declaration=True)
        print(f"Successfully removed {removed_count} unused strings from Strings.resx")
        return True
        
    except Exception as e:
        print(f"Error removing unused strings: {e}")
        return False


def main():
    detector = ProjectDetector()
    directory = detector.detect()
    
    existing = []
    found = []
    
    resx_path = Path(directory) / "Resources" / "Strings.resx"
    if not resx_path.exists():
        print(f"Strings.resx not found at {resx_path}")
        return
    
    with open(resx_path, "r", encoding="utf-8") as file:
        existing = re.findall('name="([a-zA-Z0-9.]+)" xml:space="preserve"', file.read())
    
    for filename in glob.glob(f"{directory}\\**\\*.*", recursive=True):
        if "\\bin\\" in filename or "\\obj\\" in filename or "\\Resources\\" in filename:
            continue
        
        try:
            with open(filename, "r", encoding="utf-8") as file:
                contents = file.read()
                
                matches = re.findall("Strings.([a-zA-Z0-9_]+)", contents)
                for match in matches:
                    if not "_" in match:
                        continue
                    ref = match.replace("_", ".")
                    if not ref in found:
                        found.append(ref)
                
                matches = re.findall('FromTranslation = "([a-zA-Z0-9.]+)"', contents)
                for match in matches:
                    if not match in found:
                        found.append(match)
        
        except Exception:
            continue
    
    unused = []
    for entry in existing:
        if entry not in found and not "Enums." in entry and "CustomTheme.Error" not in entry:
            unused.append(entry)
            print(entry)
    
    if unused:
        remove_unused_strings(resx_path, unused)
    else:
        print("No unused strings found.")

if __name__ == "__main__":
    main()