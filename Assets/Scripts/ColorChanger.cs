using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ColorChanger : MonoBehaviour
{
    [SerializeField] private Color rightController = Color.blue;
    [SerializeField] private Color leftController = Color.red;

    private Color ogColor;
    private Renderer renderer;

    private void Awake()
    {
        renderer = GetComponent<Renderer>();
        ogColor = renderer.material.color;
    }

    public void OnHover(HoverEnterEventArgs args)
    {
        switch (args.interactorObject.handedness)
        {
            case InteractorHandedness.Left:
                renderer.material.color = leftController;
                break;
            case InteractorHandedness.Right:
                renderer.material.color = rightController;
                break;

        }
    }
    public void OnHoverExit()
    {
        renderer.material.color = ogColor;
    }
}
