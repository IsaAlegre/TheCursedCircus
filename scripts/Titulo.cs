using Godot;

/// <summary>
/// Pantalla de título de The Cursed Circus.
/// Fondo de la carpa y la noria con zoom lento, niebla que se desplaza, título que parpadea
/// como las luces viejas de un circo, menú con botones (Continuar / Nuevo juego / Salir)
/// y un payaso que entra caminando, saluda, se ríe y se va, en bucle.
///
/// Controles del menú: W/S o flechas para elegir, Enter / Espacio / J para confirmar, o el mouse.
/// Todo se arma por código: la escena solo necesita un nodo Control con este script.
/// </summary>
public partial class Titulo : Control
{
	[Export(PropertyHint.File, "*.tscn")] public string EscenaNuevoJuego = "res://scenes/prologo.tscn";
	/// <summary>Escena a la que lleva "Continuar" (el progreso lo restaura el autoload Partida).</summary>
	[Export(PropertyHint.File, "*.tscn")] public string EscenaContinuar = "res://scenes/nivel_1.tscn";
	[Export] public string TextoTitulo = "The Cursed Circus";
	[Export] public string TextoSubtitulo = "El Circo Maldito";
	[Export] public float VolumenMusicaDb = -10f;

	[ExportGroup("Botones")]
	[Export] public Vector2 TamanioBoton = new(300f, 54f);
	[Export] public float SeparacionBotones = 14f;

	[ExportGroup("Payaso")]
	[Export] public float AltoPayaso = 230f;
	[Export] public float VelocidadPayaso = 150f;
	/// <summary>Altura de los pies en la pantalla (0 = arriba, 1 = abajo).</summary>
	[Export(PropertyHint.Range, "0,1")] public float PisoPayaso = 0.88f;
	/// <summary>Segundos de espera entre una aparición y la siguiente (mínimo y máximo).</summary>
	[Export] public Vector2 EsperaEntreApariciones = new(1.0f, 2.2f);
	[Export] public float VolumenRisaDb = -12f;

	private const string RutaFondo = "res://assets/Gemini_Generated_Image_bejzjfbejzjfbejz.jpg";
	private const string RutaNiebla = "res://assets/capa3.png";
	private const string RutaFuenteTitulo = "res://assets/fuentes/PirataOne-Regular.ttf";
	private const string RutaFuenteTexto = "res://assets/fuentes/IMFellEnglish-Regular.ttf";
	private const string RutaMusica = "res://assets/audio/musica_circo.ogg";
	private const string RutaPayasoQuieto = "res://assets/personajes/artista_quieto.png";
	private const string RutaPayasoCaminar = "res://assets/personajes/artista_caminar.png";
	private const string RutaPayasoSaludo = "res://assets/personajes/artista_aviso.png";
	private static readonly string[] RutasRisas =
	{
		"res://assets/audio/risa_payaso.wav",
		"res://assets/audio/risa_payaso_2.ogg",
		"res://assets/audio/risa_payaso_3.ogg",
	};

	private static readonly Color BlancoHueso = new("F5F2EA");
	private static readonly Color Carmesi = new("9C171F");
	private static readonly Color Violeta = new("8A2BE2");
	private static readonly Color Cian = new("3FD8EC");
	private static readonly Color Tinta = new("151515");
	private static readonly Color TonoPayaso = new(0.85f, 0.82f, 0.82f);

	private const string Continuar = "Continuar";
	private const string NuevoJuego = "Nuevo juego";
	private const string SalirDelJuego = "Salir";

	private Texture2D _fondo;
	private Texture2D _niebla;
	private Font _fuenteTitulo;
	private Font _fuenteTexto;
	private string[] _opciones = { NuevoJuego, SalirDelJuego };
	private Rect2[] _botones = new Rect2[3];
	private int _seleccion = 0;
	private float _t = 0f;
	private float _negro = 1f;
	private bool _saliendo = false;
	private float _pulsado = 0f; // animación al confirmar

	// --- Payaso ---
	private enum Fase { Esperando, Entrando, Actuando, Saliendo }
	private Fase _fase = Fase.Esperando;
	private float _tFase = 0.8f;      // la primera aparición es casi enseguida
	private float _x = 0f;
	private float _destino = 0f;
	private int _lado = 1;            // 1 = derecha, -1 = izquierda
	private Texture2D _payasoQuieto, _payasoCaminar, _payasoSaludo;
	private AudioStream[] _risas = System.Array.Empty<AudioStream>();

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		_fondo = Cargar<Texture2D>(RutaFondo);
		_niebla = Cargar<Texture2D>(RutaNiebla);
		_fuenteTitulo = Cargar<Font>(RutaFuenteTitulo) ?? ThemeDB.FallbackFont;
		_fuenteTexto = Cargar<Font>(RutaFuenteTexto) ?? ThemeDB.FallbackFont;
		_payasoQuieto = Cargar<Texture2D>(RutaPayasoQuieto);
		_payasoCaminar = Cargar<Texture2D>(RutaPayasoCaminar) ?? _payasoQuieto;
		_payasoSaludo = Cargar<Texture2D>(RutaPayasoSaludo) ?? _payasoQuieto;
		var risas = new System.Collections.Generic.List<AudioStream>();
		foreach (string ruta in RutasRisas)
		{
			var risa = Cargar<AudioStream>(ruta);
			if (risa != null)
				risas.Add(risa);
		}
		_risas = risas.ToArray();
		IniciarMusica();

		// Si hay una partida guardada, aparece "Continuar" primero.
		Node partida = GetNodeOrNull("/root/Partida");
		if (partida != null && partida.HasMethod("hay_partida_guardada") && partida.Call("hay_partida_guardada").AsBool())
			_opciones = new[] { Continuar, NuevoJuego, SalirDelJuego };
		_botones = new Rect2[_opciones.Length];

		CreateTween().TweenMethod(Callable.From<float>(v => _negro = v), 1f, 0f, 1.5f);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_t += dt;
		_pulsado = Mathf.MoveToward(_pulsado, 0f, dt * 4f);
		ActualizarPayaso(dt);
		QueueRedraw();
	}

	// ------------------------------------------------------------------
	// Menú
	// ------------------------------------------------------------------

	public override void _UnhandledInput(InputEvent evento)
	{
		if (_saliendo || evento is not InputEventKey { Pressed: true, Echo: false } tecla)
			return;
		switch (tecla.Keycode)
		{
			case Key.W or Key.Up:
				_seleccion = (_seleccion + _opciones.Length - 1) % _opciones.Length;
				break;
			case Key.S or Key.Down:
				_seleccion = (_seleccion + 1) % _opciones.Length;
				break;
			case Key.Enter or Key.KpEnter or Key.Space or Key.J:
				Confirmar();
				break;
		}
		GetViewport().SetInputAsHandled();
	}

	public override void _GuiInput(InputEvent evento)
	{
		if (_saliendo)
			return;
		if (evento is InputEventMouseMotion movimiento)
		{
			for (int i = 0; i < _botones.Length; i++)
				if (_botones[i].HasPoint(movimiento.Position))
					_seleccion = i;
		}
		else if (evento is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } clic)
		{
			for (int i = 0; i < _botones.Length; i++)
			{
				if (_botones[i].HasPoint(clic.Position))
				{
					_seleccion = i;
					Confirmar();
				}
			}
		}
	}

	private void Confirmar()
	{
		_saliendo = true;
		_pulsado = 1f;
		var tw = CreateTween();
		tw.TweenInterval(0.15f);
		tw.TweenMethod(Callable.From<float>(v => _negro = v), 0f, 1f, 1.0f);
		switch (_opciones[_seleccion])
		{
			case Continuar:
				tw.TweenCallback(Callable.From(() => GetTree().ChangeSceneToFile(EscenaContinuar)));
				break;
			case NuevoJuego:
				// El prólogo borra la partida guardada al empezar.
				tw.TweenCallback(Callable.From(() => GetTree().ChangeSceneToFile(EscenaNuevoJuego)));
				break;
			default:
				tw.TweenCallback(Callable.From(() => GetTree().Quit()));
				break;
		}
	}

	// ------------------------------------------------------------------
	// Payasos: entran, actúan y se van, rotando entre tres apariciones
	// ------------------------------------------------------------------

	private void ActualizarPayaso(float dt)
	{
		if (_payasoQuieto == null)
			return;
		Vector2 pantalla = GetViewportRect().Size;
		float fuera = AltoPayaso;
		_tFase -= dt;

		switch (_fase)
		{
			case Fase.Esperando:
				if (_tFase > 0f)
					return;
				// Entra por un costado elegido al azar.
				_lado = GD.Randf() < 0.5f ? 1 : -1;
				_x = _lado > 0 ? pantalla.X + fuera : -fuera;
				_destino = pantalla.X * (_lado > 0 ? 0.80f : 0.20f);
				_fase = Fase.Entrando;
				break;

			case Fase.Entrando:
				_x = Mathf.MoveToward(_x, _destino, VelocidadPayaso * dt);
				if (Mathf.IsEqualApprox(_x, _destino))
				{
					_fase = Fase.Actuando;
					_tFase = 2.2f;
					SonarRisa();
				}
				break;

			case Fase.Actuando:
				if (_tFase <= 0f)
					_fase = Fase.Saliendo;
				break;

			case Fase.Saliendo:
				float salida = _lado > 0 ? pantalla.X + fuera : -fuera;
				_x = Mathf.MoveToward(_x, salida, VelocidadPayaso * 1.3f * dt);
				if (Mathf.IsEqualApprox(_x, salida))
				{
					_fase = Fase.Esperando;
					_tFase = (float)GD.RandRange(EsperaEntreApariciones.X, EsperaEntreApariciones.Y);
				}
				break;
		}
	}

	private void SonarRisa()
	{
		if (_risas.Length == 0)
			return;
		var p = new AudioStreamPlayer
		{
			Stream = _risas[GD.RandRange(0, _risas.Length - 1)],
			VolumeDb = VolumenRisaDb,
			PitchScale = (float)GD.RandRange(0.9, 1.15),
		};
		AddChild(p);
		p.Play();
		p.Finished += p.QueueFree;
	}

	private void DibujarPayaso(Vector2 pantalla)
	{
		if (_payasoQuieto == null || _fase == Fase.Esperando)
			return;
		bool caminando = _fase == Fase.Entrando || _fase == Fase.Saliendo;
		Texture2D tex = _payasoQuieto;
		float salto = 0f;
		if (caminando)
		{
			tex = (int)(_t * 7f) % 2 == 0 ? _payasoCaminar : _payasoQuieto;
			salto = Mathf.Abs(Mathf.Sin(_t * 14f)) * 6f;
		}
		else
		{
			tex = (int)(_t * 3.2f) % 2 == 0 ? _payasoSaludo : _payasoQuieto;
		}

		// El dibujo original mira a la derecha: al entrar mira al centro, al salir mira afuera.
		bool haciaIzquierda = _fase == Fase.Saliendo ? _lado < 0 : _lado > 0;
		float ancho = AltoPayaso * tex.GetWidth() / tex.GetHeight();
		float piso = pantalla.Y * PisoPayaso;
		var rect = new Rect2(_x - ancho * 0.5f, piso - AltoPayaso - salto, ancho, AltoPayaso);
		if (haciaIzquierda)
			rect = new Rect2(rect.Position.X + ancho, rect.Position.Y, -ancho, rect.Size.Y);

		DrawSetTransform(new Vector2(_x, piso), 0f, new Vector2(1f, 0.25f));
		DrawCircle(Vector2.Zero, ancho * 0.45f, new Color(0f, 0f, 0f, 0.45f));
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		DrawTextureRect(tex, rect, false, TonoPayaso);
	}

	// ------------------------------------------------------------------
	// Dibujo
	// ------------------------------------------------------------------

	public override void _Draw()
	{
		Vector2 pantalla = GetViewportRect().Size;
		DrawRect(new Rect2(Vector2.Zero, pantalla), Colors.Black);

		if (_fondo != null)
		{
			float zoom = 1.04f + 0.03f * Mathf.Sin(_t * 0.15f);
			DibujarCubriendo(_fondo, pantalla, zoom, new Color(0.75f, 0.75f, 0.8f));
		}

		if (_niebla != null)
		{
			float ancho = pantalla.Y * _niebla.GetWidth() / _niebla.GetHeight();
			float x = -(_t * 25f % ancho);
			for (int i = 0; i < 3; i++)
				DrawTextureRect(_niebla, new Rect2(x + i * ancho, 0f, ancho, pantalla.Y), false, new Color(1f, 1f, 1f, 0.8f));
		}

		DibujarPayaso(pantalla);
		DibujarVineta(pantalla);
		DibujarTitulo(pantalla);
		DibujarBotones(pantalla);

		if (_negro > 0f)
			DrawRect(new Rect2(Vector2.Zero, pantalla), new Color(0f, 0f, 0f, _negro));
	}

	private void DibujarTitulo(Vector2 pantalla)
	{
		// Parpadeo irregular, como las bombillas viejas de un cartel de circo.
		float parpadeo = 0.85f + 0.15f * Mathf.Sin(_t * 2.1f);
		if (Mathf.Sin(_t * 13.7f) > 0.97f)
			parpadeo *= 0.55f;

		int tam = 92;
		float y = pantalla.Y * 0.30f;
		float ancho = _fuenteTitulo.GetStringSize(TextoTitulo, HorizontalAlignment.Left, -1, tam).X;
		var pos = new Vector2((pantalla.X - ancho) * 0.5f, y);

		for (int i = 4; i >= 1; i--)
			DrawCircle(new Vector2(pantalla.X * 0.5f, y - tam * 0.3f), 110f + i * 40f, new Color(Violeta, 0.025f * parpadeo));

		DrawString(_fuenteTitulo, pos + new Vector2(4f, 5f), TextoTitulo, HorizontalAlignment.Left, -1, tam, new Color(Carmesi, 0.9f));
		DrawStringOutline(_fuenteTitulo, pos, TextoTitulo, HorizontalAlignment.Left, -1, tam, 8, new Color(0.05f, 0.04f, 0.04f));
		DrawString(_fuenteTitulo, pos, TextoTitulo, HorizontalAlignment.Left, -1, tam, new Color(BlancoHueso, parpadeo));

		int tamSub = 26;
		float anchoSub = _fuenteTexto.GetStringSize(TextoSubtitulo, HorizontalAlignment.Left, -1, tamSub).X;
		DrawString(_fuenteTexto, new Vector2((pantalla.X - anchoSub) * 0.5f, y + 58f), TextoSubtitulo,
			HorizontalAlignment.Left, -1, tamSub, new Color(BlancoHueso, 0.75f));
	}

	/// <summary>Botones con fondo oscuro y borde de tinta; el elegido se ilumina en cian.</summary>
	private void DibujarBotones(Vector2 pantalla)
	{
		int tam = 26;
		float altoTotal = _opciones.Length * TamanioBoton.Y + (_opciones.Length - 1) * SeparacionBotones;
		float yInicial = pantalla.Y * 0.70f - altoTotal * 0.5f;

		for (int i = 0; i < _opciones.Length; i++)
		{
			bool elegido = i == _seleccion;
			var rect = new Rect2((pantalla.X - TamanioBoton.X) * 0.5f, yInicial + i * (TamanioBoton.Y + SeparacionBotones), TamanioBoton.X, TamanioBoton.Y);
			_botones[i] = rect;
			float brillo = 0.5f + 0.5f * Mathf.Sin(_t * 4f);

			if (elegido)
			{
				rect = rect.Grow(3f + _pulsado * 6f);
				// Resplandor detrás del botón elegido.
				DrawRect(rect.Grow(8f), new Color(Cian, 0.08f + 0.06f * brillo));
			}

			var estilo = new StyleBoxFlat
			{
				BgColor = elegido ? new Color(0.08f, 0.06f, 0.10f, 0.88f) : new Color(0.04f, 0.03f, 0.03f, 0.72f),
				BorderColor = elegido ? Cian : new Color(BlancoHueso, 0.35f),
			};
			estilo.SetBorderWidthAll(elegido ? 3 : 2);
			estilo.SetCornerRadiusAll(4);
			DrawStyleBox(estilo, rect);

			// Rombos violetas a los lados del elegido.
			if (elegido)
			{
				var colorRombo = new Color(Violeta, 0.6f + 0.4f * brillo);
				Rombo(new Vector2(rect.Position.X + 22f, rect.GetCenter().Y), 6f, colorRombo);
				Rombo(new Vector2(rect.End.X - 22f, rect.GetCenter().Y), 6f, colorRombo);
			}

			string texto = _opciones[i];
			Vector2 medida = _fuenteTexto.GetStringSize(texto, HorizontalAlignment.Left, -1, tam);
			var posTexto = new Vector2(rect.GetCenter().X - medida.X * 0.5f, rect.GetCenter().Y + medida.Y * 0.32f);
			DrawStringOutline(_fuenteTexto, posTexto, texto, HorizontalAlignment.Left, -1, tam, 6, new Color(0f, 0f, 0f, 0.8f));
			DrawString(_fuenteTexto, posTexto, texto, HorizontalAlignment.Left, -1, tam, elegido ? Cian : new Color(BlancoHueso, 0.75f));
		}
	}

	private void Rombo(Vector2 c, float r, Color color)
	{
		DrawColoredPolygon(new[] { c + new Vector2(0f, -r), c + new Vector2(r, 0f), c + new Vector2(0f, r), c + new Vector2(-r, 0f) }, color);
	}

	private void DibujarCubriendo(Texture2D tex, Vector2 pantalla, float zoom, Color color)
	{
		float escala = Mathf.Max(pantalla.X / tex.GetWidth(), pantalla.Y / tex.GetHeight()) * zoom;
		var tam = new Vector2(tex.GetWidth(), tex.GetHeight()) * escala;
		DrawTextureRect(tex, new Rect2((pantalla - tam) * 0.5f, tam), false, color);
	}

	/// <summary>Bordes oscuros que llevan la mirada al centro.</summary>
	private void DibujarVineta(Vector2 p)
	{
		var negro = new Color(0f, 0f, 0f, 0.85f);
		var nada = new Color(0f, 0f, 0f, 0f);
		float b = p.Y * 0.35f;
		DrawPolygon(new[] { Vector2.Zero, new Vector2(p.X, 0f), new Vector2(p.X, b), new Vector2(0f, b) }, new[] { negro, negro, nada, nada });
		DrawPolygon(new[] { new Vector2(0f, p.Y - b), new Vector2(p.X, p.Y - b), p, new Vector2(0f, p.Y) }, new[] { nada, nada, negro, negro });
		float c = p.X * 0.2f;
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
