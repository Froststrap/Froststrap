// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics.CodeAnalysis;

namespace Froststrap.Models.SettingTasks
{
    internal class EnumModPresetTask<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T> : EnumBaseTask<T> where T : struct, Enum
    {
        private readonly Dictionary<T, Dictionary<string, ModPresetFileData>> _fileDataMap = [];

        private readonly Dictionary<T, Dictionary<string, string>> _map;

        public EnumModPresetTask(string name, Dictionary<T, Dictionary<string, string>> map) : base("ModPreset", name)
        {
            _map = map;

            foreach (var enumPair in _map)
            {
                var dataMap = new Dictionary<string, ModPresetFileData>();

                foreach (var resourcePair in enumPair.Value)
                {
                    var data = new ModPresetFileData(resourcePair.Key, resourcePair.Value);

                    if (data.HashMatches() && OriginalState.Equals(default(T)))
                        OriginalState = enumPair.Key;

                    dataMap[resourcePair.Key] = data;
                }

                _fileDataMap[enumPair.Key] = dataMap;
            }
        }

        public override void Execute()
        {
            _fileDataMap.TryGetValue(OriginalState, out var originalResourceMap);
            _fileDataMap.TryGetValue(NewState, out var newResourceMap);

            if (originalResourceMap != null) foreach (var (path, data) in originalResourceMap)
            {
                if (newResourceMap == null || !newResourceMap.ContainsKey(path) && data.HashMatches())
                {
                    Filesystem.AssertReadOnly(data.FullFilePath);
                    File.Delete(data.FullFilePath);
                }
            }

            if (newResourceMap != null)
            {
                foreach (var data in newResourceMap.Values)
                {
                    if (data.HashMatches())
                        return;

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(data.FullFilePath)!
                    );

                    using var resourceStream = data.ResourceStream;
                    using var memoryStream = new MemoryStream();

                    resourceStream.CopyTo(memoryStream);

                    Filesystem.AssertReadOnly(data.FullFilePath);
                    File.WriteAllBytes(data.FullFilePath, memoryStream.ToArray());
                }
            }

            OriginalState = NewState;
        }
    }
}
