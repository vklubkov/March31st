using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace March31st {
	internal class ManifestLoader : IDisposable {
		readonly string _path;
		readonly string _originalJson;
		public ManifestInfo ManifestInfo { get; }

		public ManifestLoader(string path) {
			_path = path;
			if (!File.Exists(_path)) {
				Debug.LogError($"manifest.json file was not found at: {_path}");
				return;
			}

			_originalJson = File.ReadAllText(_path);
			ManifestInfo = JsonConvert.DeserializeObject<ManifestInfo>(_originalJson);
		}

		public void Dispose() {
			var json = JsonConvert.SerializeObject(ManifestInfo, Formatting.Indented);
			if (json == _originalJson)
				return;

			File.WriteAllText(_path, json);
		}
	}
}
