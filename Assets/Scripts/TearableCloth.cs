using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TearableCloth
{
	public class TearableCloth : MonoBehaviour
	{
		public int width = 56;
		public int height = 32;
		public float spacing = .2f;
		[Space]
		public float mass = 1f;
		public float gravity = 9.807f;
		[Space]
		public float tearDistance = 1f;
		[Header("Graphics Settings")]
		[SerializeField]
		private Material _lineMaterial;
		[SerializeField]
		private LineRenderer _lineRenderer;
		[Header("Mouse Settings")]
		public float mouseDistance = 0.15f;
		public float mouseInfluence = 0.7f;

		public class Particle
		{
			public Vector3 force;
			public Vector3 velocity;
			public Vector3 acceleration;
			public Vector3 position;
			public Vector3 previousPosition;
			public float mass;
			public float gravity;
			public bool pinned;
		}

		public class Constraint
		{
			public bool enabled;
			public Particle pointA;
			public Particle pointB;
			public float distance;
			public float maxDistance;
		}

		private bool _doUpdate;
		private float _deltaTime;
		private float _currentTime;
		private float _accumulator;

		private Vector3 _mousePosition;
		private Vector3 _previousMousePosition;

		private List<Particle> _points;
		private List<Constraint> _constraints;
		private List<LineRenderer> _lineRenderers;

		private void Awake()
		{
			_doUpdate = true;
			_deltaTime = 1 / 60f;
			_points = new List<Particle>();
			_constraints = new List<Constraint>();
			_lineRenderers = new List<LineRenderer>();

			Application.runInBackground = true;

			Initialize();
		}

		private void Update()
		{
			float newTime = Time.time;
			float frameTime = newTime - _currentTime;

			_currentTime = newTime;
			_accumulator += frameTime;

			while (_accumulator >= _deltaTime)
			{
				if (_doUpdate)
				{
					UpdateAndRender(_deltaTime);
				}

				_accumulator -= _deltaTime;
			}
		}

		private void Initialize()
		{
			int w = width - 1;
			int h = height - 1;
			float startX = (-w / 2) * spacing + spacing / 2;
			float startY = 5f;

			for (int i = 0; i < _lineRenderers.Count; ++i)
			{
				Destroy(_lineRenderers[i].gameObject);
			}

			_points.Clear();
			_constraints.Clear();
			_lineRenderers.Clear();

			for (int y = 0; y <= h; ++y)
			{
				for (int x = 0; x <= w; ++x)
				{
					List<Particle> neighbours = new List<Particle>();
					Vector3 position = new Vector3(startX + x * spacing, startY - y * spacing, 0f);

					Particle point = new Particle();
					point.position = position;
					point.previousPosition = position;
					point.mass = mass;
					point.gravity = gravity;
					point.pinned = (y == 0);

					if (x > 0)
					{
						neighbours.Add(_points[_points.Count - 1]);
					}
					if (y > 0)
					{
						neighbours.Add(_points[x + (y - 1) * (w + 1)]);
					}

					for (int i = 0; i < neighbours.Count; ++i)
					{
						Constraint constraint = new Constraint();
						constraint.enabled = true;
						constraint.pointA = point;
						constraint.pointB = neighbours[i];
						constraint.distance = spacing;
						constraint.maxDistance = tearDistance;
						_constraints.Add(constraint);

						LineRenderer lineRenderer = Instantiate(_lineRenderer, _lineRenderer.transform.parent, true);
						lineRenderer.startWidth = 0.025f;
						lineRenderer.endWidth = 0.025f;
						lineRenderer.positionCount = 2;
						lineRenderer.material = _lineMaterial;
						lineRenderer.gameObject.SetActive(true);
						_lineRenderers.Add(lineRenderer);
					}

					_points.Add(point);
				}
			}
		}

		private void UpdateAndRender(float dt)
		{
			if (Input.GetKeyDown(KeyCode.R))
			{
				Initialize();
			}

			UpdatePoints(dt);
			HandleMouseInput();
			ResolveConstraints();
			Render();
		}

		private void UpdatePoints(float dt)
		{
			for (int i = 0; i < _points.Count; ++i)
			{
				Particle point = _points[i];

				if (point.pinned)
				{
					continue;
				}

				point.force += Vector3.up * (-point.gravity * point.mass);
				point.acceleration = point.force * (1f / point.mass);
				point.velocity = (point.position - point.previousPosition) * dt;
				Vector3 nextPosition = point.position * 2 - point.previousPosition + point.acceleration * dt * dt;
				point.previousPosition = point.position;
				point.position = nextPosition;
				point.force = Vector3.zero;
			}
		}

		private void ResolveConstraints(int iterations = 5)
		{
			for (int i = 0; i < iterations; ++i)
			{
				for (int j = 0; j < _constraints.Count; ++j)
				{
					Constraint constraint = _constraints[j];

					if (!constraint.enabled)
					{
						continue;
					}

					Vector3 direction = constraint.pointA.position - constraint.pointB.position;
					float distance = direction.magnitude;

					if (distance > constraint.maxDistance)
					{
						constraint.enabled = false;
					}

					float difference = (constraint.distance - distance) / distance;
					Vector3 position = difference * 0.5f * direction;

					if (!constraint.pointA.pinned)
					{
						constraint.pointA.position += position;
					}
					if (!constraint.pointB.pinned)
					{
						constraint.pointB.position -= position;
					}
				}
			}
		}

		private void HandleMouseInput()
		{
			Vector3 mousePosition = Input.mousePosition;
			mousePosition.z = -Camera.main.transform.position.z;

			_mousePosition = Camera.main.ScreenToWorldPoint(mousePosition);

			if (Input.GetMouseButton(0))
			{
				if (_mousePosition != _previousMousePosition)
				{
					Particle referencePoint = new Particle() { position = _mousePosition };
					Particle closestPoint = _points.OrderBy(p => Vector3.Distance(p.position, referencePoint.position)).FirstOrDefault();
					
					if (closestPoint == null || closestPoint.pinned)
					{
						return;
					}

					float distance = Vector3.Distance(closestPoint.position, _mousePosition);
					if (distance < mouseDistance)
					{
						closestPoint.position += (_mousePosition - _previousMousePosition).normalized * mouseInfluence;
						_previousMousePosition = _mousePosition;
					}
				}
			}
			else
			{
				_previousMousePosition = _mousePosition;
			}

			if (Input.GetMouseButton(1))
			{
				Particle referencePoint = new Particle() { position = _mousePosition };
				Particle closestPoint = _points.OrderBy(p => Vector3.Distance(p.position, referencePoint.position)).FirstOrDefault();

				float distance = Vector3.Distance(closestPoint.position, _mousePosition);
				if (distance < mouseDistance)
				{
					Constraint[] constraints = _constraints.Where(c => c.pointA.Equals(closestPoint) || c.pointB.Equals(closestPoint)).ToArray();

					for (int i = 0; i < constraints.Length; ++i)
					{
						constraints[i].enabled = false;
					}
				}
			}
		}

		private void Render()
		{
			for (int i = 0; i < _lineRenderers.Count; ++i)
			{
				Constraint constraint = _constraints[i];
				LineRenderer lineRenderer = _lineRenderers[i];

				if (constraint.enabled)
				{
					lineRenderer.SetPosition(0, constraint.pointA.position);
					lineRenderer.SetPosition(1, constraint.pointB.position);
				}
				else
				{
					if (lineRenderer.gameObject.activeInHierarchy)
					{
						lineRenderer.gameObject.SetActive(false);
					}
				}
			}
		}
	}
}
