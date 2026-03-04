using System.Collections.Generic;
using Newtonsoft.Json;

namespace March31st {
	public class ScopedRegistryInfo {
		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("url")]
		public string Url { get; set; }

		[JsonProperty("scopes")]
		public List<string> Scopes { get; set; }
	}
}