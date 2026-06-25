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
            "fi", "fr", "hu", "id", "it",
            "ja", "ko", "lt", "lv", "nl", "pl", "pt-BR",
            "pt-PT", "ro", "ru", "sk", "sl", "sv-SE",
            "tr", "uk", "vi", "zh-CN", "zh-TW"
        ]
        
        self.qa_languages = ["da", "el", "et", "lv", "pt-PT", "sk", "sl"]
        self.skip_languages = ["en-US", "en"]
        
        self.deepl_lang_map = {
            "pt-BR": "PT-BR",
            "pt-PT": "PT-PT",
            "sv-SE": "SV",
            "zh-CN": "ZH",
            "zh-TW": "ZH-HANT",
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
            "vi": "VI",
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
        print(f"Languages to translate: {len(self.languages)}")
    
    def _escape_xml(self, text: str) -> str:
        """Escape XML special characters in text without double-escaping."""
        if not text:
            return text
        
        text = text.replace('&amp;', '\x00AMP\x00')
        text = text.replace('&lt;', '\x00LT\x00')
        text = text.replace('&gt;', '\x00GT\x00')
        text = text.replace('&quot;', '\x00QUOT\x00')
        text = text.replace('&apos;', '\x00APOS\x00')
        
        text = text.replace('&', '&amp;')
        text = text.replace('<', '&lt;')
        text = text.replace('>', '&gt;')
        text = text.replace('"', '&quot;')
        text = text.replace("'", '&apos;')
        
        text = text.replace('\x00AMP\x00', '&amp;')
        text = text.replace('\x00LT\x00', '&lt;')
        text = text.replace('\x00GT\x00', '&gt;')
        text = text.replace('\x00QUOT\x00', '&quot;')
        text = text.replace('\x00APOS\x00', '&apos;')
        
        return text
    
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
    
    def translate_text_with_retry(self, text: str, target_lang: str, max_retries: int = 2) -> str:
        """Translate text with retry logic - stops after max_retries"""
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
                
                if response.status_code == 400:
                    if attempt < max_retries - 1:
                        time.sleep(1)
                        continue
                    return text
                
                if response.status_code == 429:
                    wait_time = 5
                    print(f"    Rate limited, waiting {wait_time}s...")
                    time.sleep(wait_time)
                    continue
                
                if response.status_code >= 500:
                    if attempt < max_retries - 1:
                        time.sleep(3)
                        continue
                    return text
                
                response.raise_for_status()
                
                result = response.json()
                
                if result.get("code") != 200:
                    if attempt < max_retries - 1:
                        time.sleep(1)
                        continue
                    return text
                
                translated = result.get("data", text)
                
                if translated == text and len(text) > 3:
                    if attempt == 0:
                        print(f"    Retrying once (translation failed)...")
                        time.sleep(2)
                        continue
                    return text
                
                self.cache[cache_key] = translated
                self._save_cache()
                
                return translated
                
            except Exception as e:
                if attempt == max_retries - 1:
                    return text
                time.sleep(2)
        
        return text
    
    def translate_batch(self, texts: List[str], target_lang: str) -> Dict[str, str]:
        """Translate a batch of texts with progress reporting"""
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
            if i > 0 and i % 10 == 0:
                print(f"    Progress: {i}/{len(uncached)}")
                time.sleep(1)
            
            translated = self.translate_text_with_retry(text, target_lang)
            results[text] = translated
            
            if i < len(uncached) - 1:
                time.sleep(0.3)
        
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
            
            value_text = value.text if value.text else ""
            value_text = self._escape_xml(value_text)
            
            content += f'  <data name="{name}" xml:space="preserve">\n'
            content += f'    <value>{value_text}</value>\n'
            
            if comment is not None and comment.text:
                comment_text = comment.text
                comment_text = self._escape_xml(comment_text)
                content += f'    <comment>{comment_text}</comment>\n'
            
            content += '  </data>\n'
        
        content += '</root>'
        
        return content
    
    def translate_language(self, lang: str, base_strings: Dict[str, str]):
        if lang in self.skip_languages:
            print(f"Skipping {lang} (base English file)")
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
            value_elem.text = self._escape_xml(translated)
            
            base_tree = ET.parse(self.base_file)
            base_root = base_tree.getroot()
            base_data = base_root.find(f"./data[@name='{key}']")
            if base_data is not None:
                comment = base_data.find("comment")
                if comment is not None and comment.text:
                    comment_elem = ET.SubElement(data, "comment")
                    comment_elem.text = self._escape_xml(comment.text)
        
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
        """Start DeepLX using docker run"""
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
                print("Trying alternative DeepLX image...")
                result = subprocess.run([
                    "docker", "run", "-d",
                    "--name", "deeplx",
                    "-p", "1188:1188",
                    "--restart", "unless-stopped",
                    "missuo/deeplx:latest"
                ], capture_output=True, text=True)
                
                if result.returncode != 0:
                    print(f"Failed to start with alternative image: {result.stderr}")
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
        print(f"Translating {len(self.languages)} languages\n")
        
        for i, lang in enumerate(self.languages):
            print(f"\n[{i+1}/{len(self.languages)}]", end=" ")
            self.translate_language(lang, base_strings)
            
            if i < len(self.languages) - 1:
                print("  Waiting 3 seconds before next language...")
                time.sleep(3)
        
        total = sum(self.stats.values())
        print(f"\nComplete. Processed {total} changes across {len(self.languages)} languages")

if __name__ == "__main__":
    translator = DeepLXTranslator()
    translator.translate_all()