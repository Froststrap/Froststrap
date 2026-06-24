import os
import json
import hashlib
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, List, Set
import requests
import time
import subprocess
import sys

class DeepLXTranslator:
    def __init__(self):
        self.deeplx_url = "http://localhost:1188/translate"
        
        self.script_dir = Path(__file__).parent
        self.project_root = self.script_dir.parent.parent
        self.resources_dir = self.project_root / "Froststrap" / "Resources"
        self.base_file = self.resources_dir / "Strings.resx"
        self.cache_file = self.script_dir / "deeplx_cache.json"
        
        self.languages = [
            "ar", "bg", "cs", "da", "de", "el", "es-ES", "et",
            "fa", "fi", "fil", "fr", "hr", "hu", "id", "it",
            "ja", "ko", "lt", "lv", "ms", "nl", "pl", "pt-BR",
            "pt-PT", "ro", "ru", "sk", "sl", "sv-SE", "th",
            "tr", "uk", "vi", "zh-CN", "zh-TW"
        ]
        
        self.unsupported_languages = ["fil", "ms", "th", "vi", "fa", "hr"]
        self.qa_languages = ["da", "el", "et", "lv", "pt-PT", "sk", "sl"]
        self.skip_languages = ["en-US", "en"]
        
        self.deepl_lang_map = {
            "pt-BR": "PT-BR",
            "pt-PT": "PT-PT",
            "sv-SE": "SV",
            "zh-CN": "ZH",
            "zh-TW": "ZH",
            "es-ES": "ES",
            "ar": "AR",
            "bg": "BG",
            "cs": "CS",
            "da": "DA",
            "de": "DE",
            "el": "EL",
            "et": "ET",
            "fi": "FI",
            "fr": "FR",
            "hu": "HU",
            "id": "ID",
            "it": "IT",
            "ja": "JA",
            "ko": "KO",
            "lt": "LT",
            "lv": "LV",
            "nl": "NL",
            "pl": "PL",
            "ro": "RO",
            "ru": "RU",
            "sk": "SK",
            "sl": "SL",
            "sv": "SV",
            "tr": "TR",
            "uk": "UK",
        }
        
        self.skip_patterns = [
            r'^\{[^}]+\}$',
            r'^https?://',
            r'^\[.*\]\(.*\)$',
            r'^#.*$',
            r'^[A-Z_]+$',
        ]
        
        self.cache = self._load_cache()
        self.stats = {}
        
        print(f"DeepLX URL: {self.deeplx_url}")
        print(f"Resources: {self.resources_dir}")
        print(f"Base file: {self.base_file}")
        print(f"Cache file: {self.cache_file}")
    
    def _load_cache(self) -> Dict:
        if self.cache_file.exists():
            try:
                with open(self.cache_file, 'r', encoding='utf-8') as f:
                    return json.load(f)
            except:
                return {}
        return {}
    
    def _save_cache(self):
        with open(self.cache_file, 'w', encoding='utf-8') as f:
            json.dump(self.cache, f, ensure_ascii=False, indent=2)
    
    def _get_cache_key(self, text: str, lang: str) -> str:
        return f"{lang}|{hashlib.md5(text.encode()).hexdigest()}"
    
    def _should_skip(self, text: str) -> bool:
        if not text or len(text.strip()) < 2:
            return True
        
        import re
        for pattern in self.skip_patterns:
            if re.match(pattern, text):
                return True
        return False
    
    def _get_deepl_code(self, lang: str) -> str:
        return self.deepl_lang_map.get(lang, lang.upper())
    
    def translate_text_with_retry(self, text: str, target_lang: str, max_retries: int = 5) -> str:
        if self._should_skip(text):
            return text
        
        cache_key = self._get_cache_key(text, target_lang)
        if cache_key in self.cache:
            return self.cache[cache_key]
        
        for attempt in range(max_retries):
            try:
                target = self._get_deepl_code(target_lang)
                
                payload = {
                    "text": text,
                    "target_lang": target
                }
                
                response = requests.post(self.deeplx_url, json=payload, timeout=30)
                
                if response.status_code == 429:
                    wait_time = (2 ** attempt) * 5
                    print(f"    Rate limited, waiting {wait_time}s... (attempt {attempt + 1}/{max_retries})")
                    time.sleep(wait_time)
                    continue
                
                response.raise_for_status()
                
                result = response.json()
                translated = result.get("data", text)
                
                self.cache[cache_key] = translated
                self._save_cache()
                
                return translated
                
            except Exception as e:
                if attempt == max_retries - 1:
                    print(f"    Failed after {max_retries} attempts: {text[:30]}...")
                    return text
                
                wait_time = (2 ** attempt) * 2
                print(f"    Error, retrying in {wait_time}s... (attempt {attempt + 1}/{max_retries})")
                time.sleep(wait_time)
        
        return text
    
    def translate_batch(self, texts: List[str], target_lang: str) -> Dict[str, str]:
        if not texts:
            return {}
        
        results = {}
        uncached = []
        
        for text in texts:
            if self._should_skip(text):
                results[text] = text
                continue
            
            cache_key = self._get_cache_key(text, target_lang)
            if cache_key in self.cache:
                results[text] = self.cache[cache_key]
            else:
                uncached.append(text)
        
        if not uncached:
            return results
        
        print(f"    Translating {len(uncached)} strings...")
        
        for i, text in enumerate(uncached):
            if i > 0 and i % 5 == 0:
                print(f"    Progress: {i}/{len(uncached)}")
                time.sleep(2)
            
            translated = self.translate_text_with_retry(text, target_lang)
            results[text] = translated
        
        self._save_cache()
        return results
    
    def get_base_file_content(self) -> str:
        if not self.base_file.exists():
            print(f"Base file not found: {self.base_file}")
            return None
        
        with open(self.base_file, 'r', encoding='utf-8') as f:
            return f.read()
    
    def get_base_header(self) -> str:
        base_content = self.get_base_file_content()
        if not base_content:
            return ""
        
        schema_end = base_content.find('</xsd:schema>')
        if schema_end != -1:
            data_start = base_content.find('<data name=', schema_end)
            if data_start != -1:
                return base_content[:data_start]
        
        resheader_end = base_content.rfind('</resheader>')
        if resheader_end != -1:
            data_start = base_content.find('<data name=', resheader_end)
            if data_start != -1:
                return base_content[:data_start]
        
        data_start = base_content.find('<data name=')
        if data_start == -1:
            return base_content
        
        return base_content[:data_start]
    
    def get_strings(self) -> Dict[str, str]:
        if not self.base_file.exists():
            print(f"Base file not found: {self.base_file}")
            return {}
        
        tree = ET.parse(self.base_file)
        root = tree.getroot()
        
        strings = {}
        for data in root.findall(".//data"):
            name = data.get("name")
            value = data.find("value")
            comment = data.find("comment")
            
            if name and value is not None and value.text:
                if comment is not None and comment.text in ["Boolean", "Int32", "StringArray"]:
                    continue
                strings[name] = value.text
        
        print(f"Loaded {len(strings)} strings from base file")
        return strings
    
    def get_existing_translations(self, lang_file: Path) -> Dict[str, str]:
        if not lang_file.exists():
            return {}
        
        try:
            tree = ET.parse(lang_file)
            translations = {}
            for data in tree.getroot().findall(".//data"):
                name = data.get("name")
                value = data.find("value")
                if name and value is not None and value.text:
                    translations[name] = value.text
            return translations
        except:
            return {}
    
    def get_existing_keys(self, lang_file: Path) -> Set[str]:
        if not lang_file.exists():
            return set()
        
        try:
            tree = ET.parse(lang_file)
            keys = set()
            for data in tree.getroot().findall(".//data"):
                name = data.get("name")
                if name:
                    keys.add(name)
            return keys
        except:
            return set()
    
    def format_resx_file(self, data_elements, base_header) -> str:
        content = base_header.rstrip() + '\n'
        
        data_elements.sort(key=lambda x: x.get("name", ""))
        
        for data in data_elements:
            name = data.get("name")
            value = data.find("value")
            comment = data.find("comment")
            
            if value is None:
                continue
            
            content += f'  <data name="{name}" xml:space="preserve">\n'
            content += f'    <value>{value.text}</value>\n'
            
            if comment is not None and comment.text:
                comment_text = comment.text.replace('&', '&amp;')
                content += f'    <comment>{comment_text}</comment>\n'
            
            content += '  </data>\n'
        
        content += '</root>'
        
        return content
    
    def translate_language(self, lang: str, base_strings: Dict[str, str]):
        if lang in self.skip_languages:
            print(f"Skipping {lang} (base English file)")
            return
        
        if lang in self.unsupported_languages:
            print(f"Skipping {lang} (not supported by DeepL)")
            return
        
        lang_file = self.resources_dir / f"Strings.{lang}.resx"
        print(f"Processing {lang}...")
        
        existing_translations = self.get_existing_translations(lang_file)
        existing_keys = set(existing_translations.keys())
        base_keys = set(base_strings.keys())
        
        added_keys = base_keys - existing_keys
        removed_keys = existing_keys - base_keys
        common_keys = base_keys & existing_keys
        
        changed_keys = set()
        for key in common_keys:
            if base_strings[key] != existing_translations[key]:
                changed_keys.add(key)
        
        if not added_keys and not removed_keys and not changed_keys:
            print(f"  No changes")
            self.stats[lang] = 0
            return
        
        if added_keys:
            print(f"  Added: {len(added_keys)} new strings")
        if changed_keys:
            print(f"  Changed: {len(changed_keys)} strings updated")
        if removed_keys:
            print(f"  Removed: {len(removed_keys)} strings")
        
        need_translation = {}
        for key in added_keys:
            need_translation[key] = base_strings[key]
        for key in changed_keys:
            need_translation[key] = base_strings[key]
        
        translated_map = {}
        if need_translation:
            print(f"  Translating {len(need_translation)} strings...")
            text_list = list(need_translation.values())
            translated_map = self.translate_batch(text_list, lang)
        
        base_header = self.get_base_header()
        if not base_header:
            print(f"  Failed to read base header!")
            return
        
        tree = ET.parse(self.base_file)
        root = tree.getroot()
        
        for data in root.findall(".//data"):
            root.remove(data)
        
        for key, original in need_translation.items():
            translated = translated_map.get(original, original)
            if translated == original:
                continue
            
            data = ET.SubElement(root, "data", {"name": key, "xml:space": "preserve"})
            value_elem = ET.SubElement(data, "value")
            value_elem.text = translated
            
            base_tree = ET.parse(self.base_file)
            base_root = base_tree.getroot()
            base_data = base_root.find(f"./data[@name='{key}']")
            if base_data is not None:
                comment = base_data.find("comment")
                if comment is not None and comment.text:
                    comment_elem = ET.SubElement(data, "comment")
                    comment_elem.text = comment.text
        
        data_elements = root.findall(".//data")
        formatted_content = self.format_resx_file(data_elements, base_header)
        
        with open(lang_file, 'w', encoding='utf-8') as f:
            f.write(formatted_content)
        
        total_changes = len(added_keys) + len(changed_keys) + len(removed_keys)
        self.stats[lang] = total_changes
        print(f"  Added {len(added_keys)} new, updated {len(changed_keys)}, removed {len(removed_keys)}")
    
    def is_deeplx_running(self) -> bool:
        try:
            payload = {"text": "Hello", "target_lang": "ES"}
            response = requests.post(self.deeplx_url, json=payload, timeout=5)
            return response.status_code == 200
        except:
            return False
    
    def start_deeplx(self) -> bool:
        """Start DeepLX using docker run (no compose file needed)"""
        try:
            print("Starting DeepLX container...")
            
            subprocess.run(["docker", "rm", "-f", "deeplx"], 
                          capture_output=True, text=True)
            
            result = subprocess.run([
                "docker", "run", "-d",
                "--name", "deeplx",
                "-p", "1188:1188",
                "--restart", "unless-stopped",
                "ghcr.io/owo-network/deeplx:latest"
            ], capture_output=True, text=True)
            
            if result.returncode != 0:
                print(f"Failed to start DeepLX: {result.stderr}")
                return False
            
            print("Waiting for DeepLX to initialize...")
            time.sleep(10)
            return True
            
        except Exception as e:
            print(f"Failed to start DeepLX: {e}")
            return False
    
    def translate_all(self):
        print("\nStarting translation with DeepLX...")
        
        if not self.is_deeplx_running():
            print("DeepLX is not running. Attempting to start it...")
            if not self.start_deeplx():
                print("Failed to start DeepLX!")
                return
        
        print("DeepLX is running.\n")
        
        base_strings = self.get_strings()
        if not base_strings:
            print("No strings found!")
            return
        
        print(f"Found {len(base_strings)} strings in base file")
        
        active_languages = [lang for lang in self.languages if lang not in self.unsupported_languages]
        print(f"Translating {len(active_languages)} languages\n")
        
        for i, lang in enumerate(active_languages):
            print(f"\n[{i+1}/{len(active_languages)}]", end=" ")
            self.translate_language(lang, base_strings)
            
            if i < len(active_languages) - 1:
                print("  Waiting 5 seconds before next language...")
                time.sleep(5)
        
        total = sum(self.stats.values())
        print(f"\nComplete. Processed {total} changes across {len(active_languages)} languages")

if __name__ == "__main__":
    translator = DeepLXTranslator()
    translator.translate_all()