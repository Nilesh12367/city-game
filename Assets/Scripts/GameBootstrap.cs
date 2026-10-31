using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Builds the whole game at runtime: city, player, cars, camera, touch UI.
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        Application.targetFrameRate = 60;
        QualitySettings.shadowDistance = 80f;

        // Sun
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        CityGenerator.Build();

        // Player
        var player = PlayerController.Create(new Vector3(6f, 1f, 6f));

        // Parked cars
        for (int i = 0; i < 6; i++)
            CarController.Create(new Vector3(-3f + i * 40f, 0.6f, 3f), Quaternion.Euler(0, 90, 0));

        // Camera
        var cam = new GameObject("MainCamera").AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.farClipPlane = 600f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var follow = cam.gameObject.AddComponent<FollowCamera>();
        follow.target = player.transform;

        TouchUI.Create();
        GameManager.Instance.player = player;
        GameManager.Instance.cam = follow;
    }
}

public class GameManager
{
    static GameManager _i;
    public static GameManager Instance => _i ??= new GameManager();
    public PlayerController player;
    public FollowCamera cam;
    public CarController currentCar;

    public void ToggleCar()
    {
        if (currentCar != null)
        {
            // Exit car
            player.transform.position = currentCar.transform.position + currentCar.transform.right * -2.5f + Vector3.up;
            player.gameObject.SetActive(true);
            currentCar.driving = false;
            currentCar = null;
            cam.target = player.transform;
            cam.distance = 5f;
            return;
        }
        // Find nearby car
        CarController best = null; float bestD = 5f;
        foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(c.transform.position, player.transform.position);
            if (d < bestD) { bestD = d; best = c; }
        }
        if (best == null) return;
        currentCar = best;
        best.driving = true;
        player.gameObject.SetActive(false);
        cam.target = best.transform;
        cam.distance = 9f;
    }
}

public static class Mat
{
    public static Material Make(Color c, float smooth = 0.2f)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);
        m.color = c;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        return m;
    }
}

public static class CityGenerator
{
    public const int Blocks = 8;
    public const float BlockSize = 40f;
    public const float Road = 12f;

    public static void Build()
    {
        var root = new GameObject("City").transform;
        float total = Blocks * BlockSize;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Road";
        ground.transform.SetParent(root);
        ground.transform.localScale = new Vector3(total + 200, 1, total + 200);
        ground.transform.position = new Vector3(total / 2f, -0.5f, total / 2f);
        ground.GetComponent<Renderer>().material = Mat.Make(new Color(0.18f, 0.18f, 0.2f));

        var sidewalk = Mat.Make(new Color(0.55f, 0.55f, 0.55f));
        var grass = Mat.Make(new Color(0.25f, 0.5f, 0.22f));
        var glass = Mat.Make(new Color(0.35f, 0.5f, 0.65f), 0.85f);
        Color[] wall = { new Color(0.8f,0.75f,0.65f), new Color(0.6f,0.62f,0.66f), new Color(0.7f,0.45f,0.35f), new Color(0.9f,0.9f,0.88f) };
        var rnd = new System.Random(42);

        for (int x = 0; x < Blocks; x++)
        for (int z = 0; z < Blocks; z++)
        {
            float bx = x * BlockSize + Road / 2f + (BlockSize - Road) / 2f;
            float bz = z * BlockSize + Road / 2f + (BlockSize - Road) / 2f;
            float inner = BlockSize - Road;
            var lot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lot.transform.SetParent(root);
            lot.transform.position = new Vector3(bx, 0.15f, bz);
            lot.transform.localScale = new Vector3(inner, 0.3f, inner);
            bool park = rnd.NextDouble() < 0.12;
            lot.GetComponent<Renderer>().sharedMaterial = park ? grass : sidewalk;
            if (park)
            {
                for (int t = 0; t < 6; t++) Tree(root, new Vector3(bx + (float)(rnd.NextDouble() - 0.5) * inner * 0.8f, 0.3f, bz + (float)(rnd.NextDouble() - 0.5) * inner * 0.8f));
                continue;
            }
            // 4 buildings per block
            for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
            {
                float w = inner / 2f - 2f;
                float h = 8f + (float)rnd.NextDouble() * (rnd.NextDouble() < 0.2 ? 70f : 25f);
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Building";
                b.transform.SetParent(root);
                b.transform.position = new Vector3(bx + (i - 0.5f) * inner / 2f, h / 2f + 0.3f, bz + (j - 0.5f) * inner / 2f);
                b.transform.localScale = new Vector3(w, h, w);
                b.GetComponent<Renderer>().sharedMaterial = h > 40f ? glass : Mat.Make(wall[rnd.Next(wall.Length)]);
            }
        }
        // Street lights along roads
        for (int x = 0; x <= Blocks; x++)
        for (int z = 0; z < Blocks; z++)
            Lamp(root, new Vector3(x * BlockSize + Road / 2f - 0.5f, 0, z * BlockSize + BlockSize / 2f));
    }

    static void Tree(Transform root, Vector3 p)
    {
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(root);
        trunk.transform.position = p + Vector3.up * 1.5f;
        trunk.transform.localScale = new Vector3(0.4f, 1.5f, 0.4f);
        trunk.GetComponent<Renderer>().material = Mat.Make(new Color(0.4f, 0.27f, 0.15f));
        var top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        top.transform.SetParent(root);
        top.transform.position = p + Vector3.up * 4f;
        top.transform.localScale = Vector3.one * 3.5f;
        top.GetComponent<Renderer>().material = Mat.Make(new Color(0.15f, 0.45f, 0.18f));
    }

    static void Lamp(Transform root, Vector3 p)
    {
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.transform.SetParent(root);
        pole.transform.position = p + Vector3.up * 3f;
        pole.transform.localScale = new Vector3(0.15f, 3f, 0.15f);
        pole.GetComponent<Renderer>().material = Mat.Make(Color.gray);
    }
}

public class PlayerController : MonoBehaviour
{
    public float speed = 5f, runSpeed = 9f, jump = 5f;
    CharacterController cc;
    float vy;

    public static PlayerController Create(Vector3 pos)
    {
        var go = new GameObject("Player");
        go.transform.position = pos;
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Object.Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(go.transform, false);
        body.GetComponent<Renderer>().material = Mat.Make(new Color(0.15f, 0.3f, 0.7f));
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0, 0.95f, 0);
        head.transform.localScale = Vector3.one * 0.5f;
        head.GetComponent<Renderer>().material = Mat.Make(new Color(0.85f, 0.65f, 0.5f));
        var cc = go.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.4f;
        return go.AddComponent<PlayerController>();
    }

    void Awake() { cc = GetComponent<CharacterController>(); }

    void Update()
    {
        Vector2 input = TouchUI.Move;
        input += new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        input = Vector2.ClampMagnitude(input, 1f);
        var cam = Camera.main.transform;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        Vector3 move = fwd * input.y + right * input.x;
        float s = input.magnitude > 0.9f ? runSpeed : speed;
        if (move.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 10f * Time.deltaTime);
        if (cc.isGrounded)
        {
            vy = -1f;
            if (TouchUI.ConsumeJump() || Input.GetKeyDown(KeyCode.Space)) vy = jump;
        }
        vy += Physics.gravity.y * Time.deltaTime;
        cc.Move((move * s + Vector3.up * vy) * Time.deltaTime);
    }
}

public class CarController : MonoBehaviour
{
    public bool driving;
    public float motor = 2200f, maxSteer = 32f;
    Rigidbody rb;

    public static CarController Create(Vector3 pos, Quaternion rot)
    {
        var go = new GameObject("Car");
        go.transform.SetPositionAndRotation(pos, rot);
        Color[] colors = { Color.red, new Color(1f, 0.8f, 0.1f), Color.white, new Color(0.1f, 0.1f, 0.12f), new Color(0.2f, 0.6f, 0.3f) };
        var paint = Mat.Make(colors[Random.Range(0, colors.Length)], 0.8f);
        Part(go, PrimitiveType.Cube, new Vector3(0, 0.45f, 0), new Vector3(2f, 0.7f, 4.4f), paint);
        Part(go, PrimitiveType.Cube, new Vector3(0, 1.05f, -0.3f), new Vector3(1.8f, 0.6f, 2.2f), Mat.Make(new Color(0.2f, 0.3f, 0.4f), 0.9f));
        var tyre = Mat.Make(new Color(0.05f, 0.05f, 0.05f));
        foreach (var p in new[] { new Vector3(-1, 0, 1.4f), new Vector3(1, 0, 1.4f), new Vector3(-1, 0, -1.4f), new Vector3(1, 0, -1.4f) })
        {
            var w = Part(go, PrimitiveType.Cylinder, p + Vector3.up * 0.05f, new Vector3(0.7f, 0.15f, 0.7f), tyre);
            w.transform.localRotation = Quaternion.Euler(0, 0, 90);
        }
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.6f, 0); col.size = new Vector3(2f, 1.2f, 4.4f);
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 1200f; rb.linearDamping = 0.3f; rb.angularDamping = 3f;
        rb.centerOfMass = new Vector3(0, -0.3f, 0);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        return go.AddComponent<CarController>();
    }

    static GameObject Part(GameObject parent, PrimitiveType t, Vector3 lp, Vector3 s, Material m)
    {
        var p = GameObject.CreatePrimitive(t);
        Object.Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(parent.transform, false);
        p.transform.localPosition = lp; p.transform.localScale = s;
        p.GetComponent<Renderer>().material = m;
        return p;
    }

    void Awake() { rb = GetComponent<Rigidbody>(); }

    void FixedUpdate()
    {
        if (!driving) return;
        Vector2 input = TouchUI.Move + new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 v = rb.linearVelocity;
        float fwdSpeed = Vector3.Dot(v, transform.forward);
        // Drive
        if (fwdSpeed < 35f) rb.AddForce(transform.forward * input.y * motor * 6f);
        // Steer proportional to speed
        float turn = input.x * maxSteer * Mathf.Clamp01(Mathf.Abs(fwdSpeed) / 6f) * Mathf.Sign(fwdSpeed == 0 ? 1 : fwdSpeed);
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0, turn * Time.fixedDeltaTime * 2.5f, 0));
        // Kill sideways slide (grip)
        Vector3 side = transform.right * Vector3.Dot(v, transform.right);
        rb.AddForce(-side * rb.mass * 4f);
        // Brake
        if (TouchUI.Brake) rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 3f * Time.fixedDeltaTime);
        // Auto-upright if flipped
        if (Vector3.Dot(transform.up, Vector3.up) < 0.3f)
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
    }
}

public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5f, height = 2.2f;
    float yaw, pitch = 15f;

    void LateUpdate()
    {
        if (target == null) return;
        Vector2 look = TouchUI.Look;
        yaw += look.x * 0.25f;
        pitch = Mathf.Clamp(pitch - look.y * 0.15f, -5f, 60f);
        if (GameManager.Instance.currentCar != null && look.sqrMagnitude < 0.01f)
            yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, 3f * Time.deltaTime);
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
        Vector3 focus = target.position + Vector3.up * height * 0.6f;
        Vector3 desired = focus - rot * Vector3.forward * distance;
        if (Physics.Linecast(focus, desired, out RaycastHit hit)) desired = hit.point + hit.normal * 0.3f;
        transform.position = Vector3.Lerp(transform.position, desired, 12f * Time.deltaTime);
        transform.LookAt(focus);
    }
}

// On-screen joystick (left), look drag (right half), Jump / Car / Brake buttons.
public class TouchUI : MonoBehaviour
{
    public static Vector2 Move, Look;
    public static bool Brake;
    static bool jumpPressed;
    public static bool ConsumeJump() { bool j = jumpPressed; jumpPressed = false; return j; }

    RectTransform stickBg, stickKnob;
    int moveFinger = -1, lookFinger = -1;
    Vector2 moveStart, lastLook;

    public static void Create()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvasGo = new GameObject("TouchCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var ui = canvasGo.AddComponent<TouchUI>();
        ui.stickBg = ui.Circle(canvasGo.transform, new Vector2(260, 260), 300, new Color(1, 1, 1, 0.15f));
        ui.stickKnob = ui.Circle(ui.stickBg, Vector2.zero, 130, new Color(1, 1, 1, 0.45f));
        ui.stickKnob.anchorMin = ui.stickKnob.anchorMax = new Vector2(0.5f, 0.5f);
        ui.Button(canvasGo.transform, "JUMP", new Vector2(-180, 200), () => jumpPressed = true, null);
        ui.Button(canvasGo.transform, "CAR", new Vector2(-400, 200), () => GameManager.Instance.ToggleCar(), null);
        ui.Button(canvasGo.transform, "BRAKE", new Vector2(-180, 420), () => Brake = true, () => Brake = false);
    }

    RectTransform Circle(Transform parent, Vector2 pos, float size, Color c)
    {
        var go = new GameObject("Circle", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = false;
        return rt;
    }

    void Button(Transform parent, string label, Vector2 pos, System.Action down, System.Action up)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(190, 190);
        go.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(go.transform, false);
        t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        t.text = label; t.alignment = TextAnchor.MiddleCenter; t.fontSize = 40; t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var trig = go.AddComponent<EventTrigger>();
        var e1 = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        e1.callback.AddListener(_ => down?.Invoke()); trig.triggers.Add(e1);
        var e2 = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        e2.callback.AddListener(_ => up?.Invoke()); trig.triggers.Add(e2);
    }

    void Update()
    {
        Look = Vector2.zero;
        float scale = Screen.width / 1920f;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId);
            if (t.phase == TouchPhase.Began && !overUI)
            {
                if (t.position.x < Screen.width * 0.4f && moveFinger < 0) { moveFinger = t.fingerId; moveStart = t.position; }
                else if (lookFinger < 0) { lookFinger = t.fingerId; lastLook = t.position; }
            }
            if (t.fingerId == moveFinger)
            {
                Vector2 d = Vector2.ClampMagnitude((t.position - moveStart) / (120f * scale), 1f);
                Move = d;
                stickKnob.anchoredPosition = d * 85f;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { moveFinger = -1; Move = Vector2.zero; stickKnob.anchoredPosition = Vector2.zero; }
            }
            else if (t.fingerId == lookFinger)
            {
                Look = (t.position - lastLook) / scale;
                lastLook = t.position;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) lookFinger = -1;
            }
        }
        if (Input.touchCount == 0 && Input.GetMouseButton(1)) Look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
        if (Input.GetKeyDown(KeyCode.F)) GameManager.Instance.ToggleCar();
    }
}
