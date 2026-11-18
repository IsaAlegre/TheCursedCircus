using Godot;
using System.Collections.Generic;

/// <summary>
/// Prólogo ilustrado de The Cursed Circus: cuatro cuadros con imagen en movimiento lento
/// (zoom y paneo suave) y el texto escribiéndose sobre una franja de pergamino.
///   1. El Gran Circo en su esplendor.
///   2. El pacto del Director (usa res://assets/prologo/director.png si existe).
///   3. Los artistas transformados (con los sprites de los enemigos).
///   4. El Hechicero, con su aura violeta.
/// Cada cuadro se puede reemplazar por una ilustración propia: res://assets/prologo/cuadroN.png.
/// Al terminar, avisa al nivel que haga la entrada con la caída del Mago y cambia de escena.
///
/// Controles: cualquier tecla o clic completa el texto o pasa al cuadro siguiente. Esc salta todo.
/// Si no se toca nada, avanza solo unos segundos después de terminar cada texto.
/// </summary>
public partial class Prologo : Control
{
	[Export(PropertyHint.File, "*.tscn")] public string EscenaSiguiente = "res://scenes/nivel_1.tscn";
	[Export] public string[] Textos =
	{
		"Hubo un tiempo en que el Gran Circo era la maravilla de todos los caminos. Sus luces se veían a leguas, y nadie salía de su carpa sin haber soñado.",
		"Pero las gradas se vaciaron. Arruinado y aterrado de quedarse solo, el Director selló un pacto en la oscuridad: maldijo la carpa para que ninguno de sus artistas pudiera abandonarlo jamás.",
		"Uno a uno, acróbatas, payasos y domadores se retorcieron bajo la maldición hasta volverse criaturas encadenadas al circo.",
		"Solo uno resistió: el Hechicero del espectáculo. Atrapado entre las ruinas, le queda un único camino: adentrarse en la carpa y purificar el corazón de la maldición.",
	};
	[Export] public float VelocidadEscritura = 38f;
	/// <summary>Segundos que espera después de terminar un texto antes de avanzar solo. 0 = solo con tecla.</summary>
	[Export] public float AvanceAutomatico = 4f;
	[Export] public int TamanioTexto = 21;
	[Export] public float VolumenMusicaDb = -10f;

	// Imágenes (las que falten se reemplazan por una versión hecha con las demás).
	private const string RutaCirco = "res://assets/Gemini_Generated_Image_bejzjfbejzjfbejz.jpg";
	private const string RutaEscenario = "res://assets/fondo.png";
	private const string RutaDirector = "res://assets/prologo/director.png";
	private const string RutaMago = "res://assets/mago.png";
	private const string RutaPergamino = "res://assets/ui/pergamino.png";
	private const string RutaFuente = "res://assets/fuentes/IMFellEnglish-Regular.ttf";
	private const string RutaMusica = "res://assets/audio/musica_ambiente.ogg";
	private static readonly string[] RutasCriaturas =
	{
		"res://assets/personajes/artista_quieto.png",
		"res://assets/personajes/domador/domador_quieto_1.png",
		"res://assets/personajes/artista_aviso.png",
	};

	private static readonly Color Tinta = new("2B1A10");
	private static readonly Color Violeta = new("8A2BE2");
	private static readonly Color Carmesi = new("9C171F");

	/// <summary>Una figura dibujada encima del fondo de un cuadro.</summary>
	private sealed class Figura
	{
		public Texture2D Imagen;
		public Vector2 Posicion;   // centro de los pies, en fracción de pantalla (0-1)
		public float Alto;         // fracción del alto de pantalla
		public Color Color = Colors.White;
		public bool Voltear;
	}

	/// <summary>Un cuadro del prólogo: fondo, tinte, figuras, aura opcional y movimiento de cámara.</summary>
	private sealed class Cuadro
	{
		public Texture2D Fondo;
		public Color Tinte = Colors.White;
		public readonly List<Figura> Figuras = new();
		public Color ColorAura = new(0f, 0f, 0f, 0f);
		public Vector2 PosicionAura = new(0.5f, 0.45f);
		public float RadioAura = 0.25f;
		public Vector2 Paneo = Vector2.Zero; // desplazamiento total en píxeles durante el cuadro
	}

	private enum Fase { Entrando, Leyendo, Cambiando, Saliendo }
	private Fase _fase = Fase.Entrando;

	private readonly List<Cuadro> _cuadros = new();
	private int _indice = 0;
	private float _tCuadro = 0f;     // tiempo dentro del cuadro actual (para el zoom/paneo)
	private float _t = 0f;
	private float _negro = 1f;
	private float _esperaAvance = -1f;

	private Texture2D _pergamino;
	private Label _texto;
	private Tween _tweenTexto;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		GetNodeOrNull("/root/Partida")?.Call("borrar_checkpoint");
		IniciarMusica();
		_pergamino = Cargar<Texture2D>(RutaPergamino);
		ArmarCuadros();
		CrearTexto();
		EntrarAlCuadro(0);
	}

	private void ArmarCuadros()
	{
		Texture2D circo = Cargar<Texture2D>(RutaCirco);
		Texture2D escenario = Cargar<Texture2D>(RutaEscenario) ?? circo;
		Texture2D director = Cargar<Texture2D>(RutaDirector);
		Texture2D mago = Cargar<Texture2D>(RutaMago);

		// 1. Esplendor: la carpa con luz cálida, acercándose.
		var esplendor = new Cuadro { Fondo = circo, Tinte = new Color(1.25f, 1.08f, 0.85f), Paneo = new Vector2(-30f, 0f) };
		esplendor.ColorAura = new Color(1f, 0.75f, 0.35f, 0.10f);
		esplendor.PosicionAura = new Vector2(0.45f, 0.55f);
		_cuadros.Add(esplendor);

		// 2. El pacto: el escenario en rojo oscuro, la maldición latiendo en el centro.
		var pacto = new Cuadro { Fondo = escenario, Tinte = new Color(0.55f, 0.22f, 0.22f), Paneo = new Vector2(0f, -20f) };
		pacto.ColorAura = new Color(Carmesi, 0.22f);
		pacto.PosicionAura = new Vector2(0.5f, 0.5f);
		pacto.RadioAura = 0.3f;
		if (director != null)
			pacto.Figuras.Add(new Figura { Imagen = director, Posicion = new Vector2(0.5f, 0.92f), Alto = 0.78f });
		_cuadros.Add(pacto);

		// 3. Los artistas transformados: siluetas rojizas en la penumbra.
		var criaturas = new Cuadro { Fondo = escenario, Tinte = new Color(0.25f, 0.2f, 0.22f), Paneo = new Vector2(35f, 0f) };
		criaturas.ColorAura = new Color(Carmesi, 0.10f);
		var posiciones = new[] { new Vector2(0.28f, 0.70f), new Vector2(0.52f, 0.73f), new Vector2(0.75f, 0.70f) };
		var altos = new[] { 0.42f, 0.62f, 0.42f };
		for (int i = 0; i < RutasCriaturas.Length; i++)
		{
			Texture2D tex = Cargar<Texture2D>(RutasCriaturas[i]);
			if (tex != null)
				criaturas.Figuras.Add(new Figura { Imagen = tex, Posicion = posiciones[i], Alto = altos[i], Color = new Color(0.75f, 0.45f, 0.45f), Voltear = i == 2 });
		}
		_cuadros.Add(criaturas);

		// 4. El Hechicero: la carpa fría y oscura, el Mago al frente con su aura.
		var hechicero = new Cuadro { Fondo = circo, Tinte = new Color(0.35f, 0.36f, 0.45f), Paneo = new Vector2(-25f, 0f) };
		hechicero.ColorAura = new Color(Violeta, 0.18f);
		hechicero.PosicionAura = new Vector2(0.5f, 0.5f);
		hechicero.RadioAura = 0.22f;
		if (mago != null)
			hechicero.Figuras.Add(new Figura { Imagen = mago, Posicion = new Vector2(0.5f, 0.72f), Alto = 0.52f });
		_cuadros.Add(hechicero);

		// Ilustraciones propias: si existe res://assets/prologo/cuadro1.png (2, 3 o 4),
		// reemplaza al fondo y a las figuras de ese cuadro.
		for (int i = 0; i < _cuadros.Count; i++)
		{
			Texture2D propia = Cargar<Texture2D>($"res://assets/prologo/cuadro{i + 1}.png");
			if (propia == null)
				continue;
			_cuadros[i].Fondo = propia;
			_cuadros[i].Tinte = Colors.White;
			_cuadros[i].Figuras.Clear();
			_cuadros[i].ColorAura = new Color(_cuadros[i].ColorAura, _cuadros[i].ColorAura.A * 0.4f);
		}
	}

	private void CrearTexto()
	{
		_texto = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_texto.AddThemeColorOverride("font_color", Tinta);
		_texto.AddThemeFontSizeOverride("font_size", TamanioTexto);
		Font fuente = Cargar<Font>(RutaFuente);
		if (fuente != null)
			_texto.AddThemeFontOverride("font", fuente);
		AddChild(_texto);
	}

	// ------------------------------------------------------------------
	// Avance
	// ------------------------------------------------------------------

	private void EntrarAlCuadro(int indice)
	{
		_indice = indice;
		_tCuadro = 0f;
		_esperaAvance = -1f;
		_texto.Text = indice < Textos.Length ? Textos[indice] : "";
		_texto.VisibleRatio = 0f;
		_fase = Fase.Entrando;

		var tw = CreateTween();
		tw.TweenMethod(Callable.From<float>(v => _negro = v), _negro, 0f, 0.8f);
		tw.TweenCallback(Callable.From(() =>
		{
			_fase = Fase.Leyendo;
			_tweenTexto?.Kill();
			_tweenTexto = CreateTween();
			_tweenTexto.TweenProperty(_texto, "visible_ratio", 1f, _texto.Text.Length / VelocidadEscritura);
		}));
	}

	private void Avanzar()
	{
		if (_fase != Fase.Leyendo)
			return;
		if (_texto.VisibleRatio < 1f)
		{
			_tweenTexto?.Kill();
			_texto.VisibleRatio = 1f;
			return;
		}
		if (_indice + 1 >= _cuadros.Count)
		{
			Salir();
			return;
		}
		_fase = Fase.Cambiando;
		var tw = CreateTween();
		tw.TweenMethod(Callable.From<float>(v => _negro = v), 0f, 1f, 0.6f);
		tw.TweenCallback(Callable.From(() => EntrarAlCuadro(_indice + 1)));
	}

	private void Salir()
	{
		if (_fase == Fase.Saliendo)
			return;
		_fase = Fase.Saliendo;
		_tweenTexto?.Kill();
		// El nivel lee esta marca para hacer la entrada con la caída del Mago.
		GetTree().Root.SetMeta("intro_caida", true);
		var tw = CreateTween();
		tw.TweenMethod(Callable.From<float>(v => _negro = v), _negro, 1f, 1.0f);
		tw.TweenCallback(Callable.From(() => GetTree().ChangeSceneToFile(EscenaSiguiente)));
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_t += dt;
		_tCuadro += dt;

		if (_fase == Fase.Leyendo && _texto.VisibleRatio >= 1f && AvanceAutomatico > 0f)
		{
			if (_esperaAvance < 0f)
				_esperaAvance = AvanceAutomatico;
			_esperaAvance -= dt;
			if (_esperaAvance <= 0f)
				Avanzar();
		}

		Rect2 franja = FranjaTexto();
		_texto.Position = franja.Position + new Vector2(60f, 14f);
		_texto.Size = franja.Size - new Vector2(120f, 28f);
		_texto.Modulate = new Color(1f, 1f, 1f, 1f - _negro);
		QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent evento)
	{
		bool tecla = evento is InputEventKey { Pressed: true, Echo: false };
		bool clic = evento is InputEventMouseButton { Pressed: true };
		if (!tecla && !clic)
			return;
		GetViewport().SetInputAsHandled();
		if (evento is InputEventKey { Keycode: Key.Escape })
			Salir();
		else
			Avanzar();
	}

	// ------------------------------------------------------------------
	// Dibujo
	// ------------------------------------------------------------------

	private Rect2 FranjaTexto()
	{
		Vector2 p = GetViewportRect().Size;
		return new Rect2(90f, p.Y - 175f, p.X - 180f, 145f);
	}

	public override void _Draw()
	{
		Vector2 pantalla = GetViewportRect().Size;
		DrawRect(new Rect2(Vector2.Zero, pantalla), Colors.Black);
		if (_indice < _cuadros.Count)
			DibujarCuadro(_cuadros[_indice], pantalla);
		DibujarVineta(pantalla);
		DibujarFranja();
		DrawString(ThemeDB.FallbackFont, new Vector2(pantalla.X - 110f, 28f), "Esc: saltar",
			HorizontalAlignment.Left, -1, 14, new Color(1f, 1f, 1f, 0.35f));
		if (_negro > 0f)
			DrawRect(new Rect2(Vector2.Zero, pantalla), new Color(0f, 0f, 0f, _negro));
	}

	private void DibujarCuadro(Cuadro cuadro, Vector2 pantalla)
	{
		// Movimiento de cámara lento (efecto Ken Burns): zoom de 1.0 a 1.1 y un paneo suave.
		float avance = Mathf.Clamp(_tCuadro / 14f, 0f, 1f);
		float zoom = 1.0f + 0.1f * avance;
		Vector2 centro = pantalla * 0.5f;
		Vector2 paneo = cuadro.Paneo * avance;
		DrawSetTransformMatrix(new Transform2D(0f, new Vector2(zoom, zoom), 0f, centro + paneo - centro * zoom));

		if (cuadro.Fondo != null)
		{
			float escala = Mathf.Max(pantalla.X / cuadro.Fondo.GetWidth(), pantalla.Y / cuadro.Fondo.GetHeight());
			var tam = new Vector2(cuadro.Fondo.GetWidth(), cuadro.Fondo.GetHeight()) * escala;
			DrawTextureRect(cuadro.Fondo, new Rect2((pantalla - tam) * 0.5f, tam), false, cuadro.Tinte);
		}

		if (cuadro.ColorAura.A > 0f)
		{
			float respiro = 0.5f + 0.5f * Mathf.Sin(_t * 2f);
			Vector2 pos = cuadro.PosicionAura * pantalla;
			for (int i = 4; i >= 1; i--)
			{
				float radio = pantalla.Y * cuadro.RadioAura * (0.5f + 0.25f * i) + respiro * 8f;
				DrawCircle(pos, radio, new Color(cuadro.ColorAura, cuadro.ColorAura.A * 0.35f));
			}
		}

		foreach (Figura f in cuadro.Figuras)
		{
			float alto = pantalla.Y * f.Alto;
			var tam = new Vector2(alto * f.Imagen.GetWidth() / f.Imagen.GetHeight(), alto);
			if (f.Voltear)
				tam.X = -tam.X;
			Vector2 pies = f.Posicion * pantalla;
			DrawTextureRect(f.Imagen, new Rect2(pies.X - tam.X * 0.5f, pies.Y - alto, tam.X, alto), false, f.Color);
		}

		DrawSetTransformMatrix(Transform2D.Identity);
	}

	/// <summary>Franja de pergamino donde se escribe el texto.</summary>
	private void DibujarFranja()
	{
		Rect2 r = FranjaTexto();
		var color = new Color(1f, 1f, 1f, 0.95f);
		if (_pergamino != null)
		{
			// Franja central de la textura, con la misma proporción que el rectángulo.
			float altoFuente = _pergamino.GetWidth() * r.Size.Y / r.Size.X;
			var region = new Rect2(0f, (_pergamino.GetHeight() - altoFuente) * 0.5f, _pergamino.GetWidth(), altoFuente);
			DrawTextureRectRegion(_pergamino, r, region, color);
		}
		else
		{
			DrawRect(r, new Color("D8C49A"));
			DrawRect(r, new Color(0.1f, 0.06f, 0.04f), false, 3f);
		}

		// Indicador para seguir, cuando el texto ya terminó.
		if (_fase == Fase.Leyendo && _texto.VisibleRatio >= 1f)
		{
			float alfa = 0.5f + 0.5f * Mathf.Sin(_t * 5f);
			Vector2 c = r.End - new Vector2(30f, 24f);
			DrawColoredPolygon(new[] { c + new Vector2(-7f, -6f), c + new Vector2(7f, -6f), c + new Vector2(0f, 6f) }, new Color(Tinta, alfa));
		}
	}

	private void DibujarVineta(Vector2 p)
	{
		var negro = new Color(0f, 0f, 0f, 0.8f);
		var nada = new Color(0f, 0f, 0f, 0f);
		float b = p.Y * 0.3f;
		DrawPolygon(new[] { Vector2.Zero, new Vector2(p.X, 0f), new Vector2(p.X, b), new Vector2(0f, b) }, new[] { negro, negro, nada, nada });
		DrawPolygon(new[] { new Vector2(0f, p.Y - b), new Vector2(p.X, p.Y - b), p, new Vector2(0f, p.Y) }, new[] { nada, nada, negro, negro });
		float c = p.X * 0.18f;
		DrawPolygon(new[] { Vector2.Zero, new Vector2(c, 0f), new Vector2(c, p.Y), new Vector2(0f, p.Y) }, new[] { negro, nada, nada, negro });
		DrawPolygon(new[] { new Vector2(p.X - c, 0f), new Vector2(p.X, 0f), p, new Vector2(p.X - c, p.Y) }, new[] { nada, negro, negro, nada });
	}

	// ------------------------------------------------------------------
	// Utilidades
	// ------------------------------------------------------------------

	private void IniciarMusica()
	{
		Window raiz = GetTree().Root;
		if (raiz.HasNode("Musica") || !ResourceLoader.Exists(RutaMusica))
			return;
		var pista = (AudioStream)GD.Load<AudioStream>(RutaMusica).Duplicate();
		if (pista is AudioStreamOggVorbis ogg)
			ogg.Loop = true;
		var musica = new AudioStreamPlayer { Name = "Musica", Stream = pista, VolumeDb = VolumenMusicaDb, Autoplay = true };
		raiz.CallDeferred(Node.MethodName.AddChild, musica);
	}

	private static T Cargar<T>(string ruta) where T : class
	{
		return ResourceLoader.Exists(ruta) ? GD.Load(ruta) as T : null;
	}
}
