import os
import xml.etree.ElementTree as ET
from pathlib import Path
from collections import defaultdict
from typing import Dict, List

class ResxDuplicateFinder:
    def __init__(self):
        script_path = Path(os.path.dirname(os.path.abspath(__file__)))
        self.project_root = script_path.parent.parent
        self.resources_dir = self.project_root / "Froststrap" / "Resources"
        self.base_file = self.resources_dir / "Strings.resx"
    
    def find_duplicate_values(self, resx_path: Path) -> Dict[str, List[str]]:
        """Find duplicate string values in a RESX file."""
        if not resx_path.exists():
            print(f"Error: Strings.resx not found at {resx_path}")
            return {}
        
        try:
            tree = ET.parse(resx_path)
            root = tree.getroot()
            
            value_to_keys: Dict[str, List[str]] = defaultdict(list)
            
            for data in root.findall(".//data"):
                name = data.get("name")
                if not name:
                    continue
                
                if "Enums" in name or "Enum" in name:
                    continue
                    
                value_elem = data.find("value")
                if value_elem is None or value_elem.text is None:
                    continue
                
                value = value_elem.text.strip()
                
                if len(value) < 2:
                    continue
                
                if value.isdigit():
                    continue
                
                value_to_keys[value].append(name)
            
            duplicates = {value: keys for value, keys in value_to_keys.items() if len(keys) > 1}
            
            return duplicates
            
        except Exception as e:
            print(f"Error parsing {resx_path}: {e}")
            return {}
    
    def print_duplicates(self, duplicates: Dict[str, List[str]]):
        """Print duplicate values in a readable format."""
        if not duplicates:
            print("\nNo duplicate string values found in Strings.resx")
            return
        
        print(f"\nFile: Strings.resx")
        
        total_groups = 0
        total_entries = 0
        
        sorted_duplicates = sorted(duplicates.items(), key=lambda x: len(x[1]), reverse=True)
        
        for value, keys in sorted_duplicates:
            total_groups += 1
            total_entries += len(keys)
            
            print(f"\n  Value: \"{value}\"")
            print(f"     Used in {len(keys)} keys:")
            for key in sorted(keys):
                print(f"       - {key}")
        
        print(f"Summary:")
        print(f"  - {total_groups} duplicate value groups found")
        print(f"  - {total_entries} total duplicate entries")
        print(f"  - {len(duplicates)} unique duplicate values")

def main():
    finder = ResxDuplicateFinder()
    
    print("RESX Duplicate Value Finder")
    print()
    print(f"Looking for: {finder.base_file}")
    print()
    
    duplicates = finder.find_duplicate_values(finder.base_file)
    
    finder.print_duplicates(duplicates)

if __name__ == "__main__":
    main()