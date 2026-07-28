using SFB;

public class FileBrowserManager
{
    public string OpenFiles(string description, params string[] type)
    {
        var extensions = new[] { new ExtensionFilter(description, type) };
        var path = StandaloneFileBrowser.OpenFilePanel("Open File", "", extensions, false);
        return path.Length > 0 ? path[0] : "";
    }

    public string SelectFolder()
    {
        var path = StandaloneFileBrowser.OpenFolderPanel("Select Folder", "", false);
        return path.Length > 0 ? path[0] : "";
    }

    public string SaveFile(string description, params string[] type)
    {
        var extensionList = new[] { new ExtensionFilter(description, type) };
        return StandaloneFileBrowser.SaveFilePanel("Save File", "", "Save file", extensionList);
    }
}