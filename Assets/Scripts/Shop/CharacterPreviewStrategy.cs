using UnityEngine;

public class CharacterPreviewStrategy : IPreviewStrategy<CharacterData>
{
    private readonly Transform previewPoint;
    private GameObject currentPreview;

    public CharacterPreviewStrategy(Transform point)
    {
        previewPoint = point;
    }

    public void ShowPreview(CharacterData data)
    {
        if (currentPreview != null)
            Object.Destroy(currentPreview);

        if (data.Prefab != null)
        {
            currentPreview = Object.Instantiate(data.Prefab, previewPoint.position, Quaternion.identity, previewPoint);
        }
    }
}