namespace Prefabby
{

public interface ISettingsAccessor
{

	bool IsValid();

	string GetApiHost();
	string GetUserId();
	bool IsShowCollaboratorSelection();
	float GetHierachyCheckDeltaTime();

}

}
