using UnityEngine;

public abstract class BaseCanvas : MonoBehaviour
{
    [SerializeField] protected GameObject root;

    public virtual void Open()
    {
        if (root != null) root.SetActive(true);
        OnOpen();
    }

    public virtual void Close()
    {
        if (root != null) root.SetActive(false);
        OnClose();
    }

    public void Show() => Open();
    public void Hide() => Close();

    // Các phương thức ảo để override trong lớp con
    protected virtual void OnOpen() { }
    protected virtual void OnClose() { }

    public bool IsOpen => root != null && root.activeSelf;
}