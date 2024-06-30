using MelonLoader;
using UnityEngine;
using RumbleModdingAPI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using RumbleModUI;
using System;
using static RumbleModdingAPI.Calls;

namespace Hologram
{
    public class main : MelonMod
    {
        private bool init = false;
        private string currentScene = "Loader";
        private int sceneCount = 0;
        private bool sceneChanged = false;
        private List<GameObject> cubeGrid = new List<GameObject>();
        private Vector3[] noisePositions = new Vector3[900];
        private float time;

        public List<Vector3[]> ModelsList = new List<Vector3[]>();

        private Material material;

        private GameObject player;
        private Transform RhandTransform;
        private Transform LhandTransform;
        private Vector3 RprevHandPosition;
        private Vector3 LprevHandPosition;
        private Vector3 RhandVelocity;
        private Vector3 LhandVelocity;
        private Vector3 cubeParentVelocity = Vector3.zero;
        private float rotationVelocity;

        private GameObject RhandCube;
        private GameObject LhandCube;

        private UI UI = UI.instance;
        private Mod Hologram = new Mod();

        private string FILEPATH = "UserData\\Hologram";

        private GameObject MainParent;

        private int CurrentModel = 0;
        private int CurrentShape = 1;

        private int waitedTicks = 0;

        public GameObject cubeParent;

        private bool isAdjustingCubeCount = false;

        public override void OnLateInitializeMelon()
        {
            MelonLogger.Msg("Hologram Initiated");

            Hologram.ModName = "Hologram";
            Hologram.ModVersion = "1.2.3";
            Hologram.SetFolder("Hologram");
            Hologram.AddDescription("Description", "Description", "Shows a Hologram in Gym", new Tags
            {
                IsSummary = true
            });
            Hologram.AddToList("Follow Hands (Move)", false, 0, "Toggles if the hologram follows your hands in order to move it around", new Tags());
            Hologram.AddToList("Toggle Hologram", true, 0, "Toggles if the hologram should be enabled or not", new Tags());
            Hologram.GetFromFile();
            Hologram.ModSaved += Save;
            UI.instance.UI_Initialized += UIInit;
            Calls.onMapInitialized += OnMapInitialized;
        }

        public void UIInit()
        {
            UI.AddMod(Hologram);
        }

        public void Save()
        {
            if (currentScene == "Gym")
            {
                if ((bool)Hologram.Settings[2].SavedValue)
                {
                    MainParent.active = true;
                }
                else
                {
                    MainParent.active = false;
                }
            }
        }

        public void OnMapInitialized()
        {
            if (currentScene == "Gym")
            {
                if (!init)
                {
                    MelonCoroutines.Start(InitializeWithPause(sceneCount));
                }
                else
                {
                    MainParent.active = true;
                    if (LhandTransform == null || RhandTransform == null)
                    {
                        player = Calls.Players.GetLocalPlayer().Controller.gameObject.transform.GetChild(1).gameObject;
                        RhandTransform = player.transform.GetChild(2);
                        LhandTransform = player.transform.GetChild(1);
                    }
                }
            }
            else if (init)
            {
                MainParent.active = false;
            }
        }

        private IEnumerator InitializeWithPause(int sceneNumber)
        {
            DateTime now = DateTime.Now;
            DateTime targetTime = now.AddSeconds(1.0);
            while (DateTime.Now < targetTime)
            {
                yield return (object)new WaitForFixedUpdate();
            }
            if (sceneNumber == sceneCount)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            ModelsList = new List<Vector3[]>();

            int gridSize = 25;
                float totalVolumeSize = 1.0f;
                float spacing = totalVolumeSize / gridSize;
                float centeroffset = (gridSize - 1) * spacing / 2;
                noisePositions = new Vector3[gridSize * gridSize];
                int index = 0;
                for (int x = 0; x < gridSize; x++)
                {
                    for (int y = 0; y < gridSize; y++)
                    {
                        noisePositions[index] = new Vector3(x * spacing - centeroffset, 0f, y * spacing - centeroffset);
                        index++;
                    }
                }

                InitializeModels();
                InitializePlayer();
                InitializeHandColliders();
                InitializeCubeGrid();

                init = true;
        }

        public static string[] ReadFileText(string filePath, string fileName)
        {
            try
            {
                return File.ReadAllLines($"{filePath}\\{fileName}");
            }
            catch (System.Exception e) { MelonLogger.Error(e); }
            return null;
        }

        public static Vector3 ParseStringToVector3(string vectorString)
        {
            string[] splitString = vectorString.Split(',');
            float x = float.Parse(splitString[0]);
            float y = float.Parse(splitString[1]);
            float z = float.Parse(splitString[2]);
            return new Vector3(x, y, z);
        }

        private void InitializeModels()
        {
            string[] allFiles = Directory.GetFiles(FILEPATH, "*.obj");

            foreach (string file in allFiles)
            {
                string[] vector3s = Load3DModel(file).ToArray();
                List<Vector3> vectorList = new List<Vector3>();

                foreach (string vector3 in vector3s)
                {
                    Vector3 parsedVector = ParseStringToVector3(vector3);
                    vectorList.Add(parsedVector);
                }

                ModelsList.Add(vectorList.ToArray());
            }
        }

        List<string> Load3DModel(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MelonLogger.Error($"File Not found: {filePath}");
                return new List<string>();
            }

            List<string> vertices = new List<string>();

            string[] lines = File.ReadAllLines(filePath);
            foreach (string line in lines)
            {
                string[] parts = line.Trim().Split();
                if (parts[0] == "v")
                {
                        vertices.Add($"{parts[1]}, {parts[2]}, {parts[3]}");
                }
            }

            return vertices;
        }

        private void InitializePlayer()
        {
            player = Calls.Players.GetLocalPlayer().Controller.gameObject.transform.GetChild(1).gameObject;
            RhandTransform = player.transform.GetChild(2);
            LhandTransform = player.transform.GetChild(1);
            RprevHandPosition = RhandTransform.position;
            LprevHandPosition = LhandTransform.position;

            MainParent = new GameObject("Hologram Stuff");
            MainParent.transform.position = new Vector3(3.92f, 1.2909f, -3.1745f);
            MainParent.transform.rotation = Quaternion.Euler(0f, 18.7272f, 0f);

            UnityEngine.Object.DontDestroyOnLoad(MainParent);
        }

        private void InitializeHandColliders()
        {
            GameObject HandColliders = new GameObject("Hand Colliders");

            RhandCube = CreateHandCollider("Right Hand Collider", HandColliders.transform);
            LhandCube = CreateHandCollider("Left Hand Collider", HandColliders.transform);
            UnityEngine.Object.DontDestroyOnLoad(HandColliders);
        }

        private GameObject CreateHandCollider(string name, Transform parent)
        {
            GameObject handCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handCube.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            handCube.name = name;
            handCube.transform.parent = parent;

            Renderer renderer = handCube.GetComponent<Renderer>();
            renderer.material = Calls.Gym.GetGymPoseGhostHandler().poseGhost.transform.GetChild(0).GetChild(1).GetComponent<Renderer>().material;
            renderer.enabled = false;

            Rigidbody rb = handCube.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            handCube.AddComponent<BoxCollider>();

            return handCube;
        }

        private void InitializeCubeGrid()
        {
            cubeParent = new GameObject("CubeGridParent");
            cubeParent.transform.parent = MainParent.transform;

            material = Calls.Gym.GetGymPoseGhostHandler().poseGhost.transform.GetChild(0).GetChild(1).GetComponent<Renderer>().material;

            CreateCubeGrid(ModelsList[0], material);

            cubeParent.transform.position = Vector3.zero;
            cubeParent.transform.rotation = Quaternion.Euler(0f, 26.3635f, 0f);
            cubeParent.transform.localPosition = new Vector3(-0.2072f, -0.0909f, -0.3345f);
            cubeParent.transform.localScale = Vector3.one;

            InitializeButtons(material);
        }

        private void InitializeButtons(Material material)
        {
            GameObject nextModelButton = Calls.Create.NewButton(Vector3.zero, Quaternion.identity, () =>
            {
                if (!isAdjustingCubeCount)
                {
                    CurrentModel = (CurrentModel + 1) % (ModelsList.Count + 1);
                    AdjustCubeCountSwitch(material);
                }
            });
            nextModelButton.name = "Next Model Button";
            nextModelButton.transform.parent = MainParent.transform;
            nextModelButton.transform.localRotation = Quaternion.Euler(270.9994f, 52.2065f, 179.9999f);
            nextModelButton.transform.localPosition = new Vector3(-3.2719f, 0.0073f, -1.9171f);

            GameObject NextModelTextObject = Calls.Create.NewText("→", 1f, Color.white, nextModelButton.transform.position, Quaternion.identity);
            NextModelTextObject.name = "Next Model Text";
            NextModelTextObject.transform.parent = nextModelButton.transform;
            NextModelTextObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
            NextModelTextObject.transform.localPosition = new Vector3(0.0077f, 0.0898f, 0.0205f);

            GameObject prevModelButton = Calls.Create.NewButton(Vector3.zero, Quaternion.identity, () =>
            {
                if (!isAdjustingCubeCount)
                {
                    CurrentModel = (CurrentModel + ModelsList.Count) % (ModelsList.Count + 1);
                    AdjustCubeCountSwitch(material);
                }
            });
            prevModelButton.name = "Prev Model Button";
            prevModelButton.transform.parent = MainParent.transform;
            prevModelButton.transform.localRotation = Quaternion.Euler(270.9994f, 52.2065f, 179.9999f);
            prevModelButton.transform.localPosition = new Vector3(-3.1225f, 0.0073f, -2.1199f);

            GameObject PrevModelButtonText = Calls.Create.NewText("←", 1f, Color.white, nextModelButton.transform.position, Quaternion.identity);
            PrevModelButtonText.name = "Prev Model Text";
            PrevModelButtonText.transform.parent = prevModelButton.transform;
            PrevModelButtonText.transform.localRotation = Quaternion.Euler(90, 0, 0);
            PrevModelButtonText.transform.localPosition = new Vector3(0.0077f, 0.0898f, 0.0205f);
        }

        private void AdjustCubeCountSwitch(Material material)
        {
            if (CurrentModel >= 0 && CurrentModel < ModelsList.Count)
            {
                AdjustCubeCount(ModelsList[CurrentModel], material);
            }
            else if (CurrentModel >= 0 && CurrentModel == ModelsList.Count)
            {
                AdjustCubeCount(noisePositions, material);
            }
        }

        private void CreateCubeGrid(Vector3[] model, Material material)
        {
            PrimitiveType shapeType;
            switch (CurrentShape)
            {
                case 0:
                    shapeType = PrimitiveType.Cube;
                    break;
                case 1:
                    shapeType = PrimitiveType.Sphere;
                    break;
                default:
                    shapeType = PrimitiveType.Cube;
                    break;
            }

            foreach (Vector3 position in model)
            {
                GameObject cube = GameObject.CreatePrimitive(shapeType);
                cube.transform.localPosition = position;
                cube.transform.parent = cubeParent.transform;

                float scale = 0.03f;
                cube.transform.localScale = new Vector3(scale, scale, scale);

                Renderer renderer = cube.GetComponent<Renderer>();
                renderer.material = material;

                Rigidbody rb = cube.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                cubeGrid.Add(cube);
            }
        }

        public void AdjustCubeCount(Vector3[] model, Material material)
        {
            if (!isAdjustingCubeCount)
            {
                isAdjustingCubeCount = true;
                MelonCoroutines.Start(AdjustCubeCountCoroutine(model, material));
            }
            
        }

        private IEnumerator AdjustCubeCountCoroutine(Vector3[] model, Material material)
        {
            int currentCount = cubeGrid.Count;
            int targetCount = model.Length;
            float waitTime = 1.5f / Mathf.Abs(targetCount - currentCount);

            PrimitiveType shapeType;
            switch (CurrentShape)
            {
                case 0:
                    shapeType = PrimitiveType.Cube;
                    break;
                case 1:
                    shapeType = PrimitiveType.Sphere;
                    break;
                default:
                    shapeType = PrimitiveType.Cube;
                    break;
            }

            if (currentCount < targetCount)
            {
                for (int i = currentCount; i < targetCount; i++)
                {
                    if (currentScene != "Gym") { break; }
                        GameObject cube = GameObject.CreatePrimitive(shapeType);
                        cube.transform.localPosition = RhandTransform.position;
                        cube.transform.parent = cubeParent.transform;

                        cube.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);

                        Renderer renderer = cube.GetComponent<Renderer>();
                        renderer.material = material;

                        Rigidbody rb = cube.AddComponent<Rigidbody>();
                        rb.isKinematic = true;

                        cubeGrid.Add(cube);
                        yield return new WaitForSeconds(waitTime);
                }
            }
            else if (currentCount > targetCount)
            {
                for (int i = currentCount - 1; i >= targetCount; i--)
                {
                    GameObject cube = cubeGrid[i];

                    cubeGrid.Remove(cube);
                    GameObject.Destroy(cube);
                }
            }

            isAdjustingCubeCount = false;
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            currentScene = sceneName;
            sceneChanged = true;
        }

        public override void OnUpdate()
        {
            if (init && (bool)Hologram.Settings[2].SavedValue && currentScene == "Gym")
            {
                UpdateHandPositions();
                UpdateCubeGrid();
                UpdateNoisePositions();
            }
        }

        private void UpdateHandPositions()
        {
            time += Time.deltaTime;

            Vector3 currentRHandPosition = RhandTransform.position;
            Vector3 currentLHandPosition = LhandTransform.position;

            bool isPunchingR = Calls.ControllerMap.RightController.GetGrip() > 0.9f && Calls.ControllerMap.RightController.GetTrigger() > 0.9f;
            bool isPunchingL = Calls.ControllerMap.LeftController.GetGrip() > 0.9f && Calls.ControllerMap.LeftController.GetTrigger() > 0.9f;

            RhandCube.transform.position = currentRHandPosition;
            RhandCube.transform.rotation = RhandTransform.rotation;

            LhandCube.transform.position = currentLHandPosition;
            LhandCube.transform.rotation = LhandTransform.rotation;

            if (Vector3.Distance(player.transform.position, MainParent.transform.position) <= 4f)
            {
                if (Calls.ControllerMap.RightController.GetGrip() > 0.9f && !isPunchingR)
                {
                    RhandVelocity = (currentRHandPosition - RprevHandPosition) / Time.deltaTime;
                    rotationVelocity += RhandVelocity.x * 50f;
                }

                if (Calls.ControllerMap.LeftController.GetGrip() > 0.9f && !isPunchingL)
                {
                    LhandVelocity = (currentLHandPosition - LprevHandPosition) / Time.deltaTime;
                    rotationVelocity += LhandVelocity.x * 50f;
                }
            }

            RprevHandPosition = currentRHandPosition;
            LprevHandPosition = currentLHandPosition;

            rotationVelocity *= 0.95f;

            float maxRotationSpeed = 100f;
            rotationVelocity = Mathf.Clamp(rotationVelocity, -maxRotationSpeed, maxRotationSpeed);

            cubeParent.transform.Rotate(Vector3.up, rotationVelocity * Time.deltaTime);
        }

        private void UpdateCubeGrid()
        {
            for (int i = cubeGrid.Count - 1; i >= 0; i--)
            {
                GameObject cube = cubeGrid[i];
                if (cube != null)
                {
                    float offset = Mathf.Sin(time + i) * 0.005f;
                    Vector3 targetPosition = GetTargetPosition(i);
                    targetPosition.y += offset;

                    Rigidbody rb = cube.GetComponent<Rigidbody>();
                    HandleCollisionAndLerp(cube, rb, targetPosition);
                }
            }
        }

        private void UpdateNoisePositions()
        {
            for (int i = 0; i < noisePositions.Length; i++)
            {
                Vector3 newPosition = noisePositions[i];
                float x = newPosition.x;
                float z = newPosition.z + (time * 0.07f);

                float noise = Mathf.PerlinNoise(x / 0.2f, z / 0.2f) * 0.2f;

                newPosition.y = noise;
                noisePositions[i] = newPosition;
            }
        }

        private Vector3 GetTargetPosition(int index)
        {
            if (CurrentModel >= 0 && CurrentModel < ModelsList.Count)
            {
                return ModelsList[CurrentModel][index];
            }
            else if (CurrentModel >= 0 && CurrentModel == ModelsList.Count)
            {
                return noisePositions[index];
            }
            return Vector3.zero;
        }

        private void HandleCollisionAndLerp(GameObject cube, Rigidbody rb, Vector3 targetPosition)
        {
            // Initial setup
            rb.useGravity = false;
            rb.drag = 1f;
            rb.angularDrag = 1f;

            Collider cubeCollider = cube.GetComponent<Collider>();
            Collider rightHandCollider = RhandCube.GetComponent<Collider>();
            Collider leftHandCollider = LhandCube.GetComponent<Collider>();

            bool isLerping = false;

            // Handle collision and apply forces
            if (cubeCollider.bounds.Intersects(rightHandCollider.bounds) &&
                Calls.ControllerMap.RightController.GetGrip() > 0.9f &&
                Calls.ControllerMap.RightController.GetTrigger() > 0.9f &&
                Vector3.Distance(player.transform.position, MainParent.transform.position) <= 4f)
            {
                ApplyForce(cube, rb, (cube.transform.position - RhandCube.transform.position).normalized);
                isLerping = false;
            }
            else if (cubeCollider.bounds.Intersects(leftHandCollider.bounds) &&
                     Calls.ControllerMap.LeftController.GetGrip() > 0.9f &&
                     Calls.ControllerMap.LeftController.GetTrigger() > 0.9f &&
                     Vector3.Distance(player.transform.position, MainParent.transform.position) <= 4f)
            {
                ApplyForce(cube, rb, (cube.transform.position - LhandCube.transform.position).normalized);
                isLerping = false;
            }
            else if (rb.velocity.magnitude < 0.01f)
            {
                rb.isKinematic = true;
                isLerping = true;
            }

            // Handle lerping
            if (isLerping)
            {
                cube.transform.localPosition = Vector3.Lerp(cube.transform.localPosition, targetPosition, Time.deltaTime);
            }

            if ((bool)Hologram.Settings[1].SavedValue)
            {
                cubeParent.transform.position = RhandTransform.position;
                cubeParent.transform.rotation = Quaternion.Euler(cubeParent.transform.rotation.x, RhandTransform.rotation.y, cubeParent.transform.rotation.z);
            }
        }

        private void ApplyForce(GameObject cube, Rigidbody rb, Vector3 direction)
        {
            rb.isKinematic = false;
            rb.AddForce(direction * 5f, ForceMode.Acceleration);
        }
    }
}