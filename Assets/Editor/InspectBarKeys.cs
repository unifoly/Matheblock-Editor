using UnityEngine;
using UnityEditor;
using TMPro;
using HexMap;

public class InspectBarKeys
{
    [MenuItem("Tools/Inspect Bar Keys")]
    public static void Inspect()
    {
        var rebindButtons = Object.FindObjectsByType<RebindButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var rb in rebindButtons)
        {
            if (rb.ActionName == "Bar_Create" || rb.ActionName == "Bar_Delete")
            {
                Debug.Log($"Row: {rb.transform.parent.name} | ActionName={rb.ActionName} | DefaultKey='{rb.DefaultKey}' | KeyDisplay.text='{(rb.KeyDisplay != null ? rb.KeyDisplay.text : "NULL")}'");
                Debug.Log($"  KeyDisplay.gameObject.name='{rb.KeyDisplay?.gameObject.name}' | KeyText.parent='{rb.KeyDisplay?.transform.parent.name}'");
            }
        }
    }
}