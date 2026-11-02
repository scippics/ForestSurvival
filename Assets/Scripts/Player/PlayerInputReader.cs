using UnityEngine;
using UnityEngine.InputSystem;

// Единственное место, где читается ввод. Логика в FixedUpdate получает только эти значения,
// поэтому позже их можно будет заменить командами, пришедшими по сети.
[DefaultExecutionOrder(-100)]
public class PlayerInputReader : MonoBehaviour
{
    public Vector2 Move { get; private set; }
    public Vector2 LookDelta { get; private set; }
    public bool GrabHeld { get; private set; }
    public bool PushHeld { get; private set; }
    public bool BrakeHeld { get; private set; }

    InputActionMap map;
    InputAction move, look, grab, throwItem, push, interact, lowGear, brake, notebook, flashlight, headlights, recover, cancel;
    bool grabLatched, throwLatched, interactLatched, lowGearLatched, notebookLatched, flashlightLatched, headlightsLatched, recoverLatched;

    void Awake()
    {
        map = new InputActionMap("Player");

        move = map.AddAction("Move", InputActionType.Value);
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        look = map.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
        grab = map.AddAction("Grab", InputActionType.Button, "<Mouse>/leftButton");
        throwItem = map.AddAction("Throw", InputActionType.Button, "<Mouse>/rightButton");
        push = map.AddAction("Push", InputActionType.Button, "<Keyboard>/f");
        interact = map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
        lowGear = map.AddAction("LowGear", InputActionType.Button, "<Keyboard>/g");
        brake = map.AddAction("Brake", InputActionType.Button, "<Keyboard>/space");
        notebook = map.AddAction("Notebook", InputActionType.Button, "<Keyboard>/tab");
        flashlight = map.AddAction("Flashlight", InputActionType.Button, "<Keyboard>/t");
        headlights = map.AddAction("Headlights", InputActionType.Button, "<Keyboard>/l");
        recover = map.AddAction("Recover", InputActionType.Button, "<Keyboard>/r");
        cancel = map.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");

        map.Enable();
    }

    void OnDestroy()
    {
        map.Dispose();
    }

    void Update()
    {
        if (cancel.WasPressedThisFrame())
            GameFlow.TogglePause();

        if (GameFlow.State != GameState.Playing)
        {
            Move = Vector2.zero;
            LookDelta = Vector2.zero;
            GrabHeld = false;
            PushHeld = false;
            BrakeHeld = false;
            return;
        }

        Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        LookDelta = look.ReadValue<Vector2>();
        GrabHeld = grab.IsPressed();
        PushHeld = push.IsPressed();
        BrakeHeld = brake.IsPressed();

        // Между кадрами может не быть ни одного шага физики, поэтому нажатия
        // запоминаются до тех пор, пока их не заберёт FixedUpdate.
        grabLatched |= grab.WasPressedThisFrame();
        throwLatched |= throwItem.WasPressedThisFrame();
        interactLatched |= interact.WasPressedThisFrame();
        lowGearLatched |= lowGear.WasPressedThisFrame();
        notebookLatched |= notebook.WasPressedThisFrame();
        flashlightLatched |= flashlight.WasPressedThisFrame();
        headlightsLatched |= headlights.WasPressedThisFrame();
        recoverLatched |= recover.WasPressedThisFrame();
    }

    public bool ConsumeRecover()
    {
        bool pressed = recoverLatched;
        recoverLatched = false;
        return pressed;
    }

    public bool ConsumeFlashlight()
    {
        bool pressed = flashlightLatched;
        flashlightLatched = false;
        return pressed;
    }

    public bool ConsumeHeadlights()
    {
        bool pressed = headlightsLatched;
        headlightsLatched = false;
        return pressed;
    }

    public bool ConsumeNotebook()
    {
        bool pressed = notebookLatched;
        notebookLatched = false;
        return pressed;
    }

    public bool ConsumeInteract()
    {
        bool pressed = interactLatched;
        interactLatched = false;
        return pressed;
    }

    public bool ConsumeLowGear()
    {
        bool pressed = lowGearLatched;
        lowGearLatched = false;
        return pressed;
    }

    public bool ConsumeGrab()
    {
        bool pressed = grabLatched;
        grabLatched = false;
        return pressed;
    }

    public bool ConsumeThrow()
    {
        bool pressed = throwLatched;
        throwLatched = false;
        return pressed;
    }

}
