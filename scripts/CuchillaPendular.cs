using Godot;
using System.Collections.Generic;

/// <summary>
/// Cuchilla Pendular (GDD, Sección 6 – Obstáculos y hazards): hoja de hierro colgada de una
/// cadena que se balancea de lado a lado. Resta 1 punto de vida y exige cruzar con timing
/// (o impulsarse con el retroceso). Ciclo de 2,5 s por defecto.
///
/// Uso: agregar un Node2D al nivel, asignarle este script y poner su origen en el punto
/// de donde cuelga la cadena (el techo o una viga). En el editor se ve la cuchilla quieta
/// y, en rojo tenue, el arco que recorre al balancearse.
/// Para varias cuchillas seguidas, cambiar el Desfase de cada una (0; 0,25; 0,5…).
/// </summary>
[Tool]
public partial class CuchillaPendular : Node2D
{
	private float _largoCadena = 220f;
	private float _amplitud = 55f;
	private float _radioHoja = 48f;

	/// <summary>Distancia desde el enganche hasta el centro de la hoja.</summary>
	[Export]
	public float LargoCadena { get => _largoCadena; set { _largoCadena = Mathf.Max(20f, value); QueueRedraw(); } }

	/// <summary>Cuántos grados se aleja del centro hacia cada lado.</summary>
	[Export(PropertyHint.Range, "5,85")]
	public float Amplitud { get => _amplitud; set { _amplitud = value; QueueRedraw(); } }

	/// <summary>Tamaño de la hoja.</summary>
	[Export]
	public float RadioHoja { get => _radioHoja; set { _radioHoja = Mathf.Max(12f, value); QueueRedraw(); } }

	/// <summary>Segundos que tarda en ir y volver.</summary>
	[Export] public float Periodo = 2.5f;

	/// <summary>Momento del vaivén en que arranca (0 a 1). Sirve para desincronizar varias cuchillas.</summary>
	[Export(PropertyHint.Range, "0,1")] public float Desfase = 0f;

	[Export] public int Danio = 1;

	/// <summary>Si es true, el bloqueo perfecto la frena; por defecto solo se esquiva.</summary>
	[Export] public bool Bloqueable = false;

	/// <summary>Silbido al pasar por abajo. Vacío = usa el latigazo del Domador si existe.</summary>
	[Export] public AudioStream Sonido;
	[Export] public float VolumenDb = -8f;

	private const int CapaJugador = 2;
	private const string SonidoPorDefecto = "res://assets/audio/jefe/latigazo.wav";

	private static readonly Color Marengo = new("3A3F44");
	private static readonly Color Acero = new("5E646B");
	private static readonly Color Filo = new("D2D3D5");          // Gris Pálido
	private static readonly Color SangreVieja = new("5C1D1C");   // Rojo Granate Desgastado
	private static readonly Color Tinta = new("0E0B0B");
	private static readonly Color Arco = new(0.89f, 0.2f, 0.14f, 0.35f);  // Rojo Ardiente tenue (solo editor)

	private Area2D _hoja;
	private AudioStreamPlayer2D _audio;
	private double _t;
	private float _angulo;
	private float _anguloPrevio;

	public override void _Ready()
	{
		QueueRedraw();
		if (Engine.IsEditorHint())
			return;

		_hoja = new Area2D { Name = "Hoja", CollisionLayer = 0, CollisionMask = 0, Monitorable = false };
		_hoja.SetCollisionMaskValue(CapaJugador, true);
		_hoja.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(RadioHoja * 1.5f, RadioHoja * 0.55f) },
			Position = new Vector2(0, RadioHoja * 0.72f),
		});
		AddChild(_hoja);

		if (Sonido == null && ResourceLoader.Exists(SonidoPorDefecto))
			Sonido = GD.Load<AudioStream>(SonidoPorDefecto);
		_audio = new AudioStreamPlayer2D { Stream = Sonido, VolumeDb = VolumenDb, MaxDistance = 900f };
		AddChild(_audio);

		_t = Desfase * Periodo;
		Actualizar();
		_anguloPrevio = _angulo;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;

		_t += delta;
		Actualizar();

		// Silbido cada vez que pasa por el punto más bajo (cuando va más rápido).
		if (Sonido != null && Mathf.Sign(_angulo) != Mathf.Sign(_anguloPrevio))
		{
			_audio.PitchScale = (float)GD.RandRange(0.9, 1.1);
			_audio.Play();
		}
		_anguloPrevio = _angulo;

		foreach (Node2D cuerpo in _hoja.GetOverlappingBodies())
		{
			if (cuerpo.IsInGroup("jugador") && cuerpo.HasMethod("recibir_danio"))
				cuerpo.Call("recibir_danio", Danio, _hoja, Bloqueable);
		}
		QueueRedraw();
	}

	private void Actualizar()
	{
		_angulo = Mathf.DegToRad(Amplitud) * Mathf.Sin(Mathf.Tau * (float)(_t / Periodo));
		_hoja.Position = PosicionHoja(_angulo);
		_hoja.Rotation = _angulo;
	}

	private Vector2 PosicionHoja(float angulo) => new Vector2(0, LargoCadena).Rotated(angulo);

	public override void _Draw()
	{
		if (Engine.IsEditorHint())
		{
			// Recorrido del vaivén, para ubicarla sin tener que probar el juego.
			float amp = Mathf.DegToRad(Amplitud);
			DrawArc(Vector2.Zero, LargoCadena, Mathf.Pi / 2f - amp, Mathf.Pi / 2f + amp, 32, Arco, 3f);
			DrawArc(Vector2.Zero, LargoCadena + RadioHoja, Mathf.Pi / 2f - amp, Mathf.Pi / 2f + amp, 32, Arco, 1.5f);
		}

		Vector2 hoja = PosicionHoja(_angulo);

		// Cadena: eslabones alternados (de frente y de canto).
		int eslabones = Mathf.Max(2, (int)(LargoCadena / 14f));
		for (int i = 0; i < eslabones; i++)
		{
			Vector2 p = hoja * ((i + 0.5f) / eslabones);
			DrawSetTransform(p, _angulo);
			if (i % 2 == 0)
				DrawRect(new Rect2(-3.5f, -7f, 7f, 14f), Marengo, false, 2.5f);
			else
				DrawLine(new Vector2(0, -7f), new Vector2(0, 7f), Marengo, 3f);
		}

		// Enganche al techo.
		DrawSetTransform(Vector2.Zero, 0f);
		var enganche = new Rect2(-16f, -12f, 32f, 14f);
		DrawRect(enganche, Marengo);
		DrawRect(enganche, Tinta, false, 2.5f);
		DrawCircle(new Vector2(-9f, -5f), 2.5f, Tinta);
		DrawCircle(new Vector2(9f, -5f), 2.5f, Tinta);

		// Hoja en media luna.
		DrawSetTransform(hoja, _angulo);
		float r = RadioHoja;
		DrawLine(Vector2.Zero, new Vector2(0, r * 0.6f), Tinta, 9f);
		DrawLine(Vector2.Zero, new Vector2(0, r * 0.6f), Marengo, 5f);

		var contorno = new List<Vector2>();
		var filo = new List<Vector2>();
		const int pasos = 16;
		for (int k = 0; k <= pasos; k++)
		{
			float ang = Mathf.DegToRad(Mathf.Lerp(15f, 165f, k / (float)pasos));
			var punto = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
			contorno.Add(punto);
			filo.Add(punto * 0.94f);
		}
		for (int k = pasos; k >= 0; k--)
		{
			float ang = Mathf.DegToRad(Mathf.Lerp(15f, 165f, k / (float)pasos));
			contorno.Add(new Vector2(Mathf.Cos(ang) * 0.78f, Mathf.Sin(ang) * 0.55f) * r);
		}
		DrawColoredPolygon(contorno.ToArray(), Acero);
		DrawPolyline(filo.ToArray(), Filo, 2f);
		DrawCircle(new Vector2(r * 0.32f, r * 0.74f), r * 0.11f, SangreVieja);
		DrawCircle(new Vector2(r * 0.48f, r * 0.62f), r * 0.07f, SangreVieja);
		DrawCircle(new Vector2(-r * 0.2f, r * 0.83f), r * 0.05f, SangreVieja);
		contorno.Add(contorno[0]);
		DrawPolyline(contorno.ToArray(), Tinta, 2.5f);

		DrawCircle(Vector2.Zero, 8f, Tinta);
		DrawCircle(Vector2.Zero, 5f, Marengo);
		DrawSetTransform(Vector2.Zero, 0f);
	}
}
