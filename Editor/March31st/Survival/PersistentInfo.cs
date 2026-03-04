namespace March31st {
	public enum State {
		None,
		AddScopedRegistry,
		InstallNuGetForUnity,
		InstallTabula,
		SetScriptingDefine
	}

	public class PersistentInfo {
		public static PersistentInfo Default => new();
		public State State { get; set; }
	}
}