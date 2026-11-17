using Godot;

/// <summary>
/// Malabarista Corrupto: enemigo secundario a distancia de la Zona 1 (bestiario del GDD).
/// Se queda en su palco haciendo malabares. Cuando ve al Mago, avisa (se pone rojo, ríe)
/// y le lanza una clava en arco. La clava se puede esquivar o devolver con un bloqueo perfecto.
///
/// Imágenes opcionales (si no existen, usa el sprite del Artista Corrupto teñido):
///   res://assets/personajes/malabarista_quieto.png
///   res://assets/personajes/malabarista_lanzar.png
/// </summary>
public partial class Malabarista : CharacterBody2D
{
	[Export] public int Vida = 20;
	[Export] public float RangoVision = 560f;
	[Export] public float DuracionAviso = 0.6f;
	[Export] public float TiempoEntreLanzamientos = 2.2f;
	/// <summary>Alto del dibujo en pantalla, en píxeles.</summary>
	[Export] public float AltoSprite = 136f;
	[Export] public bool SpriteMiraALaDerecha = true;
	[Export] public float Gravedad = 1200f;
	[Export] public float VolumenEfectosDb = -4f;

	[ExportGroup("Botín")]
	[Export(PropertyHint.Range, "0,1")] public float ProbCristal = 1f;

	private static readonly Color Tinte = new(0.75f, 0.8f, 1.05f);
	private static readonly Color RojoAviso = new(1.7f, 0.55f, 0.55f);
	private static readonly Color BlancoHueso = new("F5F2EA");
	private static readonly Color Carmesi = new("9C171F");
	private static readonly Color Tinta = new("151515");

	private enum Estado { Malabares, Aviso, Lanzando, Muerto }
	private Estado _estado = Estado.Malabares;

	private Texture2D _texQuieto;
	private Texture2D _texLanzar;
	private AudioStream _sfxRisa;
	private AudioStream _sfxImpacto;
	private AudioStream _sfxMuerte;
	private Node2D _jugador;
	private CollisionShape2D _colision;
	private float _t = 0f;
	private float _tEstado = 0f;
	private float _tEspera = 1f;
	private int _mirando = -1;
	private float _destello = 0f;

	public override void _Ready()
	{
		AddToGroup("enemigos");
		CollisionLayer = 0;
		SetCollisionLayerValue(3, true);  // enemigos
		CollisionMask = 0;
		SetCollisionMaskValue(1, true);   // mundo
		SetCollisionMaskValue(5, true);   // plataformas semisólidas (el palco)

		_colision = new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(44f, AltoSprite * 0.8f) },
			Position = new Vector2(0f, -AltoSprite * 0.4f),
		};
		AddChild(_colision);

		_texQuieto = Cargar<Texture2D>("res://assets/personajes/malabarista_quieto.png");
		_texLanzar = Cargar<Texture2D>("res://assets/personajes/malabarista_lanzar.png");
		if (_texQuieto == null)
		{
			_texQuieto = Cargar<Texture2D>("res://assets/personajes/artista_quieto.png");
			_texLanzar ??= Cargar<Texture2D>("res://assets/personajes/artista_aviso.png");
		}
		_texLanzar ??= _texQuieto;

		_sfxRisa = Cargar<AudioStream>("res://assets/audio/risa_payaso_2.ogg") ?? Cargar<AudioStream>("res://assets/audio/risa_payaso.wav");
		_sfxImpacto = Cargar<AudioStream>("res://assets/audio/impacto_enemigo.wav");
		_sfxMuerte = Cargar<AudioStream>("res://assets/audio/muerte_enemigo.wav");
	}

	/// <summary>Centro del cuerpo (lo usan los proyectiles y la clava devuelta).</summary>
	public Vector2 centro() => _colision.GlobalPosition;

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_t += dt;
		_destello = Mathf.MoveToward(_destello, 0f, dt * 6f);
		if (_estado == Estado.Muerto)
			return;

		Vector2 v = Velocity;
		v.X = 0f;
		if (!IsOnFloor())
			v.Y += Gravedad * dt;
		Velocity = v;
		MoveAndSlide();

		_tEstado += dt;
		switch (_estado)
		{
			case Estado.Malabares:
				_tEspera -= dt;
				if (_tEspera <= 0f && VeAlJugador())
				{
					MirarAlJugador();
					CambiarEstado(Estado.Aviso);
					Sonido(_sfxRisa, 0f, 0.15f, 1.25f);
				}
				break;
			case Estado.Aviso:
				MirarAlJugador();
				if (_tEstado >= DuracionAviso)
				{
					Lanzar();
					CambiarEstado(Estado.Lanzando);
				}
				break;
			case Estado.Lanzando:
				if (_tEstado >= 0.35f)
				{
					_tEspera = TiempoEntreLanzamientos;
					CambiarEstado(Estado.Malabares);
				}
				break;
		}
		QueueRedraw();
	}

	private void CambiarEstado(Estado nuevo)
	{
		_estado = nuevo;
		_tEstado = 0f;
	}

	private bool VeAlJugador()
	{
		if (_jugador == null || !IsInstanceValid(_jugador))
			_jugador = GetTree().GetFirstNodeInGroup("jugador") as Node2D;
		if (_jugador == null)
			return false;
		Vector2 d = PosicionJugador() - centro();
		return Mathf.Abs(d.X) < RangoVision && Mathf.Abs(d.Y) < 400f;
	}

	private Vector2 PosicionJugador()
	{
		return _jugador.HasMethod("centro") ? _jugador.Call("centro").AsVector2() : _jugador.GlobalPosition;
	}

	private void MirarAlJugador()
	{
		if (_jugador != null && IsInstanceValid(_jugador))
			_mirando = PosicionJugador().X > centro().X ? 1 : -1;
	}

	/// <summary>Lanza una clava en arco que cae sobre la posición actual del Mago.</summary>
	private void Lanzar()
	{
		if (_jugador == null || !IsInstanceValid(_jugador))
			return;
		var clava = new Clava { Lanzador = this };
		Vector2 desde = centro() + new Vector2(_mirando * 20f, -AltoSprite * 0.35f);
		Vector2 hasta = PosicionJugador();
		float tiempo = Mathf.Clamp(desde.DistanceTo(hasta) / 450f, 0.6f, 1.3f);
		// Tiro parabólico: llega a "hasta" en "tiempo" segundos con gravedad.
		Vector2 d = hasta - desde;
		clava.Velocidad = new Vector2(d.X / tiempo, (d.Y - 0.5f * clava.Gravedad * tiempo * tiempo) / tiempo);
		GetParent().AddChild(clava);
		clava.GlobalPosition = desde;
	}

	// ------------------------------------------------------------------
	// Lo llaman los proyectiles del Mago y la clava devuelta
	// ------------------------------------------------------------------

	public bool recibir_danio(int cantidad, Node2D origen)
	{
		if (_estado == Estado.Muerto)
			return false;
		Vida -= cantidad;
		_destello = 1f;
		if (Vida > 0)
		{
			Sonido(_sfxImpacto, 0f, 0.1f);
			// Si le pegan, se apura a responder.
			_tEspera = Mathf.Min(_tEspera, 0.4f);
			return true;
		}
		Morir();
		return true;
	}

	private void Morir()
	{
		_estado = Estado.Muerto;
		Sonido(_sfxMuerte, 0f, 0.05f, 1f, true);
		_colision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		SoltarCristal();
		var tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), 0.6f);
		tw.TweenCallback(Callable.From(QueueFree));
	}

	private void SoltarCristal()
	{
		if (GD.Randf() > ProbCristal)
			return;
		var script = Cargar<Script>("res://scripts/cristal_mana.gd");
		if (script == null)
			return;
		var cristal = new Area2D();
		cristal.SetScript(script);
		// Siempre al menos mediano: es un enemigo más difícil que el Artista.
		cristal.Set("tamanio", GD.Randf() < 0.2f ? 2 : 1);
		Node nivel = GetParent();
		cristal.Position = nivel is Node2D nivel2D ? nivel2D.ToLocal(centro()) : centro();
		nivel.CallDeferred(Node.MethodName.AddChild, cristal);
	}

	// ------------------------------------------------------------------
	// Presentación
	// ------------------------------------------------------------------

	public override void _Draw()
	{
		Texture2D tex = _estado == Estado.Aviso || _estado == Estado.Lanzando ? _texLanzar : _texQuieto;
		Color color = _estado == Estado.Aviso ? RojoAviso : Tinte;
		color = color.Lerp(new Color(3f, 3f, 3f), _destello * 0.7f);

		if (tex != null)
		{
			float ancho = AltoSprite * tex.GetWidth() / tex.GetHeight();
			bool voltear = (_mirando < 0) == SpriteMiraALaDerecha;
			// Tiembla durante el aviso, como el Artista, para que se lea el ataque.
			float temblor = _estado == Estado.Aviso ? (float)GD.RandRange(-2.0, 2.0) : 0f;
			var rect = new Rect2(-ancho * 0.5f + temblor, -AltoSprite, ancho, AltoSprite);
			if (voltear)
				rect = new Rect2(rect.Position.X + ancho, rect.Position.Y, -ancho, rect.Size.Y);
			DrawTextureRect(tex, rect, false, color);
		}
		else
		{
			DrawRect(new Rect2(-22f, -AltoSprite, 44f, AltoSprite), new Color("3B2A4A"));
		}

		// Tres clavas girando sobre su cabeza mientras hace malabares.
		if (_estado == Estado.Malabares)
		{
			var centroMalabares = new Vector2(0f, -AltoSprite - 28f);
			for (int i = 0; i < 3; i++)
			{
				float a = _t * 5f + i * Mathf.Tau / 3f;
				var p = centroMalabares + new Vector2(Mathf.Cos(a) * 24f, Mathf.Sin(a) * 16f);
				DibujarClava(p, a * 2f);
			}
		}
	}

	private void DibujarClava(Vector2 pos, float giro)
	{
		DrawSetTransform(pos, giro, Vector2.One * 0.8f);
		DrawRect(new Rect2(-2f, 3f, 4f, 10f), BlancoHueso);
		var cuerpo = new Vector2[10];
		for (int i = 0; i < 10; i++)
		{
			float a = Mathf.Tau * i / 10f;
			cuerpo[i] = new Vector2(Mathf.Cos(a) * 6f, Mathf.Sin(a) * 9f - 3f);
		}
		DrawColoredPolygon(cuerpo, BlancoHueso);
		DrawRect(new Rect2(-6f, -5f, 12f, 3f), Carmesi);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	private void Sonido(AudioStream stream, float volumenExtra = 0f, float variacion = 0f, float tono = 1f, bool enElNivel = false)
	{
		if (stream == null)
			return;
		var p = new AudioStreamPlayer2D
		{
			Stream = stream,
			VolumeDb = VolumenEfectosDb + volumenExtra,
			PitchScale = tono + (float)GD.RandRange(-variacion, variacion),
			MaxDistance = 1400f,
		};
		if (enElNivel)
		{
			GetParent().AddChild(p);
			p.GlobalPosition = centro();
		}
		else
		{
			AddChild(p);
			p.Position = _colision.Position;
		}
		p.Play();
		p.Finished += p.QueueFree;
	}

	private static T Cargar<T>(string ruta) where T : class
	{
		return ResourceLoader.Exists(ruta) ? GD.Load(ruta) as T : null;
	}
}
