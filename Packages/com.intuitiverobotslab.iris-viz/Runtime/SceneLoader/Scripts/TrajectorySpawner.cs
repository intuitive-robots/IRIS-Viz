using System.Collections.Generic;
using IRIS.Node;
using IRIS.Utilities;
using MessagePack;
using UnityEngine;

namespace IRIS.SceneLoader
{
    [MessagePackObject]
    public class TrajectoryWaypoint
    {
        [Key("pos")]
        public float[] pos; // Position [x, y, z]

        [Key("color")]
        public float[] color; // RGBA [r, g, b, a]
    }

    [MessagePackObject]
    public class TrajectoryConfig
    {
        [Key("name")]
        public string name;

        [Key("waypoints")]
        public TrajectoryWaypoint[] waypoints; // Array of waypoints with position and color

        [Key("width")]
        public float width;

        [Key("resolution")]
        public int resolution; // Interpolation segments per control point (default 10)
    }

    public class TrajectorySpawner : Singleton<TrajectorySpawner>
    {
        private Dictionary<string, GameObject> _trajectoryDict = new();

        [SerializeField] private Material lineMaterial;

        void Start()
        {
            IRISXRNode.Instance.ServiceManager.RegisterServiceCallback<TrajectoryConfig, string>(
                "SpawnTrajectory", SpawnTrajectory);
            IRISXRNode.Instance.ServiceManager.RegisterServiceCallback<TrajectoryConfig, string>(
                "UpdateTrajectory", UpdateTrajectory);
            IRISXRNode.Instance.ServiceManager.RegisterServiceCallback<string, string>(
                "DeleteTrajectory", DeleteTrajectory);
        }

        private string SpawnTrajectory(TrajectoryConfig config)
        {
            UnityMainThreadDispatcher.Instance.EnqueueAndWait(() =>
            {
                if (_trajectoryDict.ContainsKey(config.name))
                {
                    Debug.LogWarning($"Trajectory with name {config.name} already exists, replacing it.");
                    Destroy(_trajectoryDict[config.name]);
                    _trajectoryDict.Remove(config.name);
                }

                GameObject trajectoryObj = CreateTrajectoryObject(config);
                _trajectoryDict.Add(config.name, trajectoryObj);
            });
            return ResponseStatus.SUCCESS;
        }

        private string UpdateTrajectory(TrajectoryConfig config)
        {
            if (!_trajectoryDict.ContainsKey(config.name))
            {
                Debug.LogWarning($"Trajectory with name {config.name} does not exist.");
                return "Trajectory Not Found";
            }

            UnityMainThreadDispatcher.Instance.EnqueueAndWait(() =>
            {
                if (_trajectoryDict.TryGetValue(config.name, out GameObject existingObj))
                {
                    LineRenderer lineRenderer = existingObj.GetComponent<LineRenderer>();
                    if (lineRenderer != null)
                    {
                        UpdateLineRenderer(lineRenderer, config);
                    }
                }
            });
            return ResponseStatus.SUCCESS;
        }

        private string DeleteTrajectory(string name)
        {
            if (!_trajectoryDict.ContainsKey(name))
            {
                Debug.LogWarning($"Trajectory with name {name} does not exist, ignoring.");
                return "Trajectory Not Found";
            }

            UnityMainThreadDispatcher.Instance.EnqueueAndWait(() =>
            {
                if (_trajectoryDict.TryGetValue(name, out GameObject obj))
                {
                    Destroy(obj);
                    _trajectoryDict.Remove(name);
                }
            });
            return ResponseStatus.SUCCESS;
        }

        private GameObject CreateTrajectoryObject(TrajectoryConfig config)
        {
            GameObject obj = new GameObject($"Trajectory_{config.name}");
            obj.transform.SetParent(transform);

            LineRenderer lineRenderer = obj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer);
            UpdateLineRenderer(lineRenderer, config);

            return obj;
        }

        private void ConfigureLineRenderer(LineRenderer lineRenderer)
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.allowOcclusionWhenDynamic = false;

            if (lineMaterial != null)
            {
                lineRenderer.material = lineMaterial;
            }
            else
            {
                // Use default unlit color material
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
        }

        private void UpdateLineRenderer(LineRenderer lineRenderer, TrajectoryConfig config)
        {
            // Extract positions and colors from waypoints
            Vector3[] controlPoints = ExtractPositions(config.waypoints);
            Color[] controlColors = ExtractColors(config.waypoints);

            // Interpolate B-spline curve
            int resolution = config.resolution > 0 ? config.resolution : 10;
            Vector3[] curvePoints = InterpolateBSpline(controlPoints, resolution);

            // Set positions
            lineRenderer.positionCount = curvePoints.Length;
            lineRenderer.SetPositions(curvePoints);

            // Set width
            float width = config.width > 0 ? config.width : 0.01f;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;

            // Set per-point colors
            if (controlColors.Length > 0)
            {
                // Interpolate colors along the B-spline
                Color[] curveColors = InterpolateColors(controlColors, resolution, controlPoints.Length);

                // Create gradient from interpolated colors
                Gradient gradient = CreateGradientFromColors(curveColors);
                lineRenderer.colorGradient = gradient;
            }
            else
            {
                Debug.LogWarning($"Trajectory {lineRenderer.gameObject.name} has no color information, defaulting to white.");
                // Default white color
                lineRenderer.startColor = Color.white;
                lineRenderer.endColor = Color.white;
            }
        }

        private Vector3[] ExtractPositions(TrajectoryWaypoint[] waypoints)
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return new Vector3[0];
            }

            Vector3[] result = new Vector3[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i]?.pos != null && waypoints[i].pos.Length >= 3)
                {
                    result[i] = new Vector3(waypoints[i].pos[0], waypoints[i].pos[1], waypoints[i].pos[2]);
                }
            }
            return result;
        }

        private Color[] ExtractColors(TrajectoryWaypoint[] waypoints)
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return new Color[] { Color.white };
            }

            Color[] result = new Color[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i]?.color != null && waypoints[i].color.Length >= 3)
                {
                    result[i] = new Color(
                        waypoints[i].color[0],
                        waypoints[i].color[1],
                        waypoints[i].color[2],
                        waypoints[i].color.Length >= 4 ? waypoints[i].color[3] : 1f
                    );
                }
                else
                {
                    result[i] = Color.white;
                }
            }
            return result;
        }

        private Color[] InterpolateColors(Color[] controlColors, int resolution, int numControlPoints)
        {
            if (controlColors == null || controlColors.Length == 0)
            {
                return new Color[] { Color.white };
            }

            if (controlColors.Length == 1)
            {
                return controlColors;
            }

            // Calculate total number of curve points (same logic as InterpolateBSpline)
            int totalPoints;
            if (numControlPoints < 4)
            {
                totalPoints = (numControlPoints - 1) * resolution + 1;
            }
            else
            {
                int numSpans = numControlPoints - 3;
                totalPoints = numSpans * resolution + 1;
            }

            Color[] result = new Color[totalPoints];
            for (int i = 0; i < totalPoints; i++)
            {
                float t = i / (float)(totalPoints - 1);
                float colorIndex = t * (controlColors.Length - 1);
                int idx0 = Mathf.FloorToInt(colorIndex);
                int idx1 = Mathf.Min(idx0 + 1, controlColors.Length - 1);
                float blend = colorIndex - idx0;
                result[i] = Color.Lerp(controlColors[idx0], controlColors[idx1], blend);
            }

            return result;
        }

        private Gradient CreateGradientFromColors(Color[] colors)
        {
            Gradient gradient = new Gradient();

            // Unity Gradient supports max 8 color keys
            int maxKeys = Mathf.Min(colors.Length, 8);
            GradientColorKey[] colorKeys = new GradientColorKey[maxKeys];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[maxKeys];

            for (int i = 0; i < maxKeys; i++)
            {
                float t = i / (float)(maxKeys - 1);
                int colorIdx = Mathf.RoundToInt(t * (colors.Length - 1));
                colorKeys[i] = new GradientColorKey(colors[colorIdx], t);
                alphaKeys[i] = new GradientAlphaKey(colors[colorIdx].a, t);
            }

            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }

        /// <summary>
        /// Interpolates a cubic B-spline curve from control points.
        /// </summary>
        /// <param name="controlPoints">Array of control points</param>
        /// <param name="segmentsPerSpan">Number of segments to generate per control point span</param>
        /// <returns>Array of interpolated curve points</returns>
        private Vector3[] InterpolateBSpline(Vector3[] controlPoints, int segmentsPerSpan)
        {
            if (controlPoints == null || controlPoints.Length < 2)
            {
                return controlPoints ?? new Vector3[0];
            }

            // For fewer than 4 points, use linear interpolation
            if (controlPoints.Length < 4)
            {
                return InterpolateLinear(controlPoints, segmentsPerSpan);
            }

            List<Vector3> curvePoints = new List<Vector3>();

            // Number of curve spans is (n - 3) for cubic B-spline with n control points
            int numSpans = controlPoints.Length - 3;

            for (int span = 0; span < numSpans; span++)
            {
                Vector3 p0 = controlPoints[span];
                Vector3 p1 = controlPoints[span + 1];
                Vector3 p2 = controlPoints[span + 2];
                Vector3 p3 = controlPoints[span + 3];

                for (int seg = 0; seg < segmentsPerSpan; seg++)
                {
                    float t = seg / (float)segmentsPerSpan;
                    Vector3 point = EvaluateCubicBSpline(p0, p1, p2, p3, t);
                    curvePoints.Add(point);
                }
            }

            // Add the last point
            float tLast = 1f;
            Vector3 lastPoint = EvaluateCubicBSpline(
                controlPoints[controlPoints.Length - 4],
                controlPoints[controlPoints.Length - 3],
                controlPoints[controlPoints.Length - 2],
                controlPoints[controlPoints.Length - 1],
                tLast
            );
            curvePoints.Add(lastPoint);

            return curvePoints.ToArray();
        }

        /// <summary>
        /// Evaluates a point on a cubic B-spline segment.
        /// Uses the cubic B-spline basis matrix.
        /// </summary>
        private Vector3 EvaluateCubicBSpline(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            // Cubic B-spline basis functions
            float b0 = (-t3 + 3f * t2 - 3f * t + 1f) / 6f;
            float b1 = (3f * t3 - 6f * t2 + 4f) / 6f;
            float b2 = (-3f * t3 + 3f * t2 + 3f * t + 1f) / 6f;
            float b3 = t3 / 6f;

            return b0 * p0 + b1 * p1 + b2 * p2 + b3 * p3;
        }

        /// <summary>
        /// Linear interpolation fallback for fewer than 4 control points.
        /// </summary>
        private Vector3[] InterpolateLinear(Vector3[] controlPoints, int segmentsPerSpan)
        {
            if (controlPoints.Length < 2)
            {
                return controlPoints;
            }

            List<Vector3> result = new List<Vector3>();

            for (int i = 0; i < controlPoints.Length - 1; i++)
            {
                for (int seg = 0; seg < segmentsPerSpan; seg++)
                {
                    float t = seg / (float)segmentsPerSpan;
                    result.Add(Vector3.Lerp(controlPoints[i], controlPoints[i + 1], t));
                }
            }

            // Add the last point
            result.Add(controlPoints[controlPoints.Length - 1]);

            return result.ToArray();
        }

        /// <summary>
        /// Gets a trajectory GameObject by name.
        /// </summary>
        public GameObject GetTrajectory(string name)
        {
            return _trajectoryDict.TryGetValue(name, out GameObject obj) ? obj : null;
        }

        /// <summary>
        /// Clears all trajectories.
        /// </summary>
        public void ClearAllTrajectories()
        {
            foreach (var kvp in _trajectoryDict)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            _trajectoryDict.Clear();
        }
    }
}
