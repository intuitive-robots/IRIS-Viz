using System.Collections.Generic;
using IRIS.Node;
using IRIS.Utilities;
using MessagePack;
using UnityEngine;

namespace IRIS.SceneLoader
{
    [MessagePackObject]
    public class TrajectoryConfig
    {
        [Key("name")]
        public string name;

        [Key("points")]
        public float[][] points; // B-spline control points [[x,y,z], ...]

        [Key("color")]
        public float[] color; // RGBA [r,g,b,a]

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
                Debug.Log($"Trajectory with name {name} does not exist, ignoring.");
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
            // Convert control points to Vector3 array
            Vector3[] controlPoints = ConvertToVector3Array(config.points);

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

            // Set color
            if (config.color != null && config.color.Length >= 3)
            {
                Color color = new Color(
                    config.color[0],
                    config.color[1],
                    config.color[2],
                    config.color.Length >= 4 ? config.color[3] : 1f
                );
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }
        }

        private Vector3[] ConvertToVector3Array(float[][] points)
        {
            if (points == null || points.Length == 0)
            {
                return new Vector3[0];
            }

            Vector3[] result = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null && points[i].Length >= 3)
                {
                    result[i] = new Vector3(points[i][0], points[i][1], points[i][2]);
                }
            }
            return result;
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
