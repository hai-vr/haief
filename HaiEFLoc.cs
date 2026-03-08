using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HVR.EF.Loc
{
    // HEFLoc V0.1.9000
    public class HaiEFLoc
    {
        private readonly string _root;

        private const string LocalizationPrefs = "HVR.EF.Localization.language";
        private const string MissingLocalizationKeyPrefix = "__";
        private const string LanguageLabel = "Language";

        private LocalizationData _loaded;
        private readonly List<LocalizationData> _availableLanguages = new();
        
        private int _selected;
        private readonly GUIContent[] _selector;

        public HaiEFLoc(string root, string folder)
        {
            _root = root;

            var assets = AssetDatabase.FindAssets("", new[] { folder });
            var localizationPaths = assets
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".json"))
                .ToList();

            var requestedLanguage = EditorPrefs.GetString(LocalizationPrefs, "en");
            
            foreach (var localizationPath in localizationPaths)
            {
                var parsed = TryParsePathOrNull(localizationPath);
                if (parsed != null)
                {
                    if (parsed.Language == "en")
                    {
                        _availableLanguages.Insert(0, parsed);
                    }
                    else
                    {
                        _availableLanguages.Add(parsed);
                    }
                }
            }

            _selector = _availableLanguages.Select(data => new GUIContent(data.CleanName)).ToArray();
            TryApplyRequestedLanguage(requestedLanguage);
        }

        private void TryApplyRequestedLanguage(string requestedLanguage)
        {
            for (var index = 0; index < _availableLanguages.Count; index++)
            {
                var localizationData = _availableLanguages[index];
                if (localizationData.Language == requestedLanguage)
                {
                    _loaded = localizationData;
                    _selected = index;
                    return;
                }
            }

            _loaded = _availableLanguages[0];
            _selected = 0;
        }

        private LocalizationData TryParsePathOrNull(string assetPath)
        {
            try
            {
                var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                var all = JObject.Parse(textAsset.text);
                var meta = all["_meta"]!.ToObject<JObject>();
                
                var ours = all[_root]!.ToObject<JObject>();

                var language = meta["language"]!.Value<string>();
                var name = meta["name"]!.Value<string>();
                var variant = meta["variant"]!.Value<string>();

                Dictionary<string, string> data = new();
                foreach (var (key, value) in ours["phrases"]!.ToObject<JObject>())
                {
                    data.Add($"phrases.{key}", value!.Value<string>());
                }
                foreach (var (enumKey, enumValues) in ours["enums"]!.ToObject<JObject>())
                {
                    foreach (var (key, value) in enumValues!.ToObject<JObject>())
                    {
                        data.Add($"enums.{enumKey}.{key}", value!.Value<string>());
                    }
                }

                return new LocalizationData(language, name, variant, data);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        public void PropertyField(string localizationKey, SerializedProperty property)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(Text(localizationKey)));
        }

        public void EnumPropertyField<TEnum>(string localizationKey, SerializedProperty property)
        {
            var newValue = EditorGUILayout.Popup(new GUIContent(Text(localizationKey)), property.intValue, property.enumNames.Select(
                (enumName, i) => LocalizeEnumName(typeof(TEnum).Name, enumName)).ToArray());
            if (newValue != property.intValue)
            {
                property.intValue = newValue;
            }
        }

        public string Text(string localizationKey)
        {
            return DoLocalize($"phrases.{localizationKey}");
        }

        private string LocalizeEnumName(string enumType, string enumValue)
        {
            return DoLocalize($"enums.{enumType}.{enumValue}");
        }

        private string DoLocalize(string actualKey)
        {
            if (_loaded.Data.TryGetValue(actualKey, out var value)) return value;
            
            return MissingLocalizationKeyPrefix + actualKey;
        }

        public void Selector()
        {
            EditorGUILayout.Separator();
            var newSelected = EditorGUILayout.Popup(new GUIContent(LanguageLabel), _selected, _selector);
            if (newSelected != _selected)
            {
                _selected = newSelected;
                var requestedLanguage = _availableLanguages[_selected].Language;
                TryApplyRequestedLanguage(requestedLanguage);
                
                EditorPrefs.SetString(LocalizationPrefs, requestedLanguage);
            }
        }
    }

    internal class LocalizationData
    {
        public string Language { get; }
        public string Name { get; }
        public string Localizer { get; }
        public Dictionary<string, string> Data { get; }
        public string CleanName { get; }

        public LocalizationData(string language, string name, string localizer, Dictionary<string, string> data)
        {
            Language = language;
            Name = name;
            Localizer = localizer;
            Data = data;
            CleanName = localizer == "" ? name : $"{name} ({localizer})";
        }
    }
}