namespace Prefabby
{

class Region
{

	public static string[] DeriveRegionOptions()
	{
		if (Constants.local)
		{
			#pragma warning disable CS0162
			return new string[]{ "(please select)", "Local" };
		}
		else if (Constants.devel)
		{
			#pragma warning disable CS0162
			return new string[]{ "(please select)", "Devel" };
		}
		else
		{
			#pragma warning disable CS0162
			return new string[]{ "(please select)", "Falkenstein (Germany)", "Sydney (Australia)" };
		}
	}

	public static int DeriveRegion(string apiHost)
	{
		string[] options = DeriveRegionOptions();
		for (int i = 0; i < options.Length; ++i)
		{
			string test = DeriveApiHost(i);
			if (test == apiHost)
			{
				return i;
			}
		}
		throw new System.Exception($"Unable to derive region from host {apiHost}!");
	}

	public static string DeriveApiHost(int region)
	{
		if (Constants.local)
		{
			#pragma warning disable CS0162
			if (region == 1)
			{
				return "http://localhost:8080";
			}
			else
			{
				return "";
			}
		}
		else if (Constants.devel)
		{
			#pragma warning disable CS0162
			if (region == 1)
			{
				return "https://devel.prefabby.com";
			}
			else
			{
				return "";
			}
		}
		else
		{
			#pragma warning disable CS0162
			switch (region)
			{
				case 0:
					return "";
				case 1:
					return "https://fsn.app.prefabby.com";
				case 2:
					return "https://syd.app.prefabby.com";
				default:
					throw new System.Exception("Unsupported region!");
			}
		}
	}

}

}
