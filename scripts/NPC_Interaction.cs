using Godot;

/// <summary>
/// NPC_Interaction (Fase 5): permite hablar con el NPC.
/// Va en un Area2D hijo del NPC, más grande que su cuerpo
/// (equivale al BoxCollider2D con "Is Trigger" de Unity).
///
/// Cuando el Mago entra al área aparece una "E" sobre el NPC.
/// Con E se abre el diálogo; cada E avanza una línea (o completa la que se está escribiendo).
/// Al hablar, la cámara se acerca, el resto de la escena se oscurece y queda una luz sobre el NPC.
/// Mientras el texto se escribe, se escucha su voz (susurro). Si el Mago se aleja, todo se cierra.
///
/// Equivalencias con Unity:
///   OnTriggerEnter2D / Exit2D   → señales BodyEntered / BodyExited
///   CompareTag("Player")        → IsInGroup("jugador") (el Mago se agrega a ese grupo)
///   Canvas + Panel + Text       → CanvasLayer + Panel + Label
///   Input.GetKeyDown(KeyCode.E) → acción "interactuar" (se crea sola con la tecla E)
/// </summary>
public partial class NPC_Interaction : Area2D
{
	[Export] public Panel PanelDialogo;
	[Export] public Label TextoDialogo;
	/// <summary>Opcional: Label donde se muestra el nombre del NPC.</summary>
	[Export] public Label TextoNombre;
	[Export] public string NombreNpc = "Ofelia, la Adivina";
	[Export] public string[] Dialogo =
	{
		"Otro hechicero... Las cartas te anunciaron hace tres noches. Ni ellas creyeron que llegarías tan lejos.",
		"Yo vi venir la maldición y se lo advertí al Director. Se rió... igual que ríen ahora sus artistas.",
		"El Domador guarda una llama que no le pertenece. Arrebátasela, y ningún telón volverá a cerrarte el paso.",
	};
	/// <summary>Altura del indicador "E" sobre el origen del NPC (sus pies).</summary>
	[Export] public float AlturaIndicador = 170f;
	/// <summary>Caracteres por segundo del efecto máquina de escribir.</summary>
	[Export] public float VelocidadEscritura = 35f;

	[ExportGroup("Cuadro de diálogo")]
	/// <summary>Da al panel el estilo del juego (fondo oscuro, borde cian) y lo ubica en pantalla.</summary>
	[Export] public bool AplicarEstilo = true;
	/// <summary>Cuadro arriba de la pantalla (true) o abajo (false).</summary>
	[Export] public bool Arriba = true;
	[Export] public float AltoPanel = 140f;
	/// <summary>Espacio libre a cada costado (más grande = más angosto). Arriba, con 340 no tapa el HUD.</summary>
	[Export] public float MargenCostados = 340f;
	/// <summary>Distancia del cuadro al borde de la pantalla.</summary>
	[Export] public float MargenBorde = 24f;
	[Export] public int TamanioTexto = 18;
	[Export] public int TamanioNombre = 24;
	/// <summary>Tipografía del nombre (.ttf u .otf). Si queda vacío se usa
	/// res://assets/fuentes/PirataOne-Regular.ttf (si existe).</summary>
	[Export] public Font FuenteNombre;
	/// <summary>Tipografía del texto. Si queda vacío se usa
	/// res://assets/fuentes/IMFellEnglish-Regular.ttf (si existe).</summary>
	[Export] public Font FuenteTexto;

	[ExportGroup("Retrato")]
	/// <summary>Muestra la cara del NPC en un recuadro a la izquierda del texto.</summary>
	[Export] public bool MostrarRetrato = true;
	/// <summary>Imagen del retrato. Si queda vacío se usa res://assets/personajes/ofelia_retrato.png;
	/// y si tampoco existe, se recorta la cara de la imagen de frente (o la normal) del NPC.</summary>
	[Export] public Texture2D Retrato;
	/// <summary>Al recortar: qué parte del alto de la imagen ocupa la cara (desde arriba).</summary>
	[Export(PropertyHint.Range, "0.1,1")] public float AltoCara = 0.3f;
	/// <summary>Al recortar: cuánto bajar el recorte desde el borde de arriba (fracción del alto).</summary>
	[Export(PropertyHint.Range, "0,0.8")] public float BajarRecorte = 0.02f;
	/// <summary>Al recortar: correr el recorte a los costados (fracción del ancho, negativo = izquierda).</summary>
	[Export(PropertyHint.Range, "-0.5,0.5")] public float CorrerRecorte = 0f;

	[ExportGroup("Enfoque al hablar")]
	/// <summary>Acerca la cámara y oscurece todo menos al NPC mientras habla.</summary>
	[Export] public bool EnfocarAlHablar = true;
	/// <summary>1 = sin zoom. 1.6 = 60 % más cerca.</summary>
	[Export] public float ZoomEnfoque = 1.4f;
	/// <summary>Mueve el encuadre: negativo = más arriba, positivo = más abajo (en píxeles del nivel).</summary>
	[Export] public float AjusteVertical = -40f;
	/// <summary>0 = sin oscurecer, 1 = negro total alrededor de la luz.</summary>
	[Export(PropertyHint.Range, "0,1")] public float OscuridadEnfoque = 0.7f;
	/// <summary>Tamaño del círculo de luz (fracción del alto de la pantalla).</summary>
	[Export(PropertyHint.Range, "0.05,0.6")] public float RadioLuz = 0.22f;
	[Export] public float DuracionEnfoque = 0.5f;

	[ExportGroup("Voz")]
	/// <summary>Una grabación por línea de diálogo (la voz diciendo esa frase). Si queda vacío
	/// se cargan solas res://assets/audio/ofelia_linea_1.ogg, _2, _3... (también .wav o .mp3).
	/// Las líneas sin grabación usan los susurros de "Voces".</summary>
	[Export] public AudioStream[] VocesPorLinea = System.Array.Empty<AudioStream>();
	/// <summary>Si hay grabación, el texto se escribe al ritmo de la voz.</summary>
	[Export] public bool SincronizarTextoConVoz = true;
	[Export] public float VolumenLineaDb = -2f;
	/// <summary>Susurros que se turnan mientras se escribe. Si queda vacío se cargan
	/// res://assets/audio/voz_ofelia_susurro_1.ogg a _6 (o, si faltan, voz_ofelia_1.wav, _2 y _3).</summary>
	[Export] public AudioStream[] Voces = System.Array.Empty<AudioStream>();
	[Export] public float VolumenVoz = -10f;
	/// <summary>Variación del tono entre un susurro y otro.</summary>
	[Export] public float VariacionTono = 0.08f;

	private static readonly Color Tinta = new("0E0B0B");
	private static readonly Color Cian = new("3FD8EC");
	private static readonly Color BlancoHueso = new("F5F2EA");

	private const string ShaderLuz = @"
shader_type canvas_item;
uniform vec2 centro = vec2(0.5);   // posición de la luz (0-1 en pantalla)
uniform float aspecto = 1.777;     // ancho / alto de la pantalla
uniform float radio = 0.22;        // fracción del alto de pantalla
uniform float oscuridad = 0.0;
void fragment() {
	vec2 d = (UV - centro) * vec2(aspecto, 1.0);
	float luz = smoothstep(radio, radio * 1.9, length(d));
	COLOR = vec4(0.01, 0.015, 0.03, luz * oscuridad);
}";

	private bool _jugadorEnRango = false;
	private Node2D _jugador;
	private int _linea = -1; // -1 = diálogo cerrado
	private Tween _tweenTexto;
	private NPC_Controller _npc;
	private float _t;

	private AudioStreamPlayer _reproductorVoz;
	private AudioStreamPlayer _reproductorLinea;

	private ColorRect _capaLuz;
	private ShaderMaterial _materialLuz;
	private Camera2D _camara;
	private Vector2 _zoomOriginal = Vector2.One;
	private Vector2 _offsetOriginal = Vector2.Zero;
	private Tween _tweenEnfoque;

	public override void _Ready()
	{
		// Solo detecta al jugador (capa 2).
		CollisionLayer = 0;
		CollisionMask = 0;
		SetCollisionMaskValue(2, true);
		BodyEntered += AlEntrar;
		BodyExited += AlSalir;

		_npc = GetParent() as NPC_Controller;
		CrearAccionInteractuar();
		PrepararVoz();
		CrearCapaLuz();

		if (PanelDialogo != null)
		{
			// El diálogo se dibuja por encima de la oscuridad y del HUD.
			if (PanelDialogo.GetParent() is CanvasLayer capaDialogo)
				capaDialogo.Layer = 20;
			// Diferido: así el NPC ya cargó sus imágenes (las necesita el retrato).
			Callable.From(() =>
			{
				if (AplicarEstilo)
					Estilizar();
				AplicarFuentes();
			}).CallDeferred();
			PanelDialogo.Visible = false;
		}
	}

	private static void CrearAccionInteractuar()
	{
		if (InputMap.HasAction("interactuar"))
			return;
		InputMap.AddAction("interactuar");
		InputMap.ActionAddEvent("interactuar", new InputEventKey { PhysicalKeycode = Key.E });
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_t += dt;
		if (_jugadorEnRango && Input.IsActionJustPressed("interactuar"))
			Avanzar();
		ActualizarVoz(dt);
		ActualizarLuz();
		QueueRedraw();
	}

	// ------------------------------------------------------------------
	// Diálogo
	// ------------------------------------------------------------------

	private void Avanzar()
	{
		if (PanelDialogo == null || TextoDialogo == null || Dialogo == null || Dialogo.Length == 0)
		{
			GD.PushWarning($"{Name}: falta asignar PanelDialogo, TextoDialogo o el texto del diálogo.");
			return;
		}

		// Si la línea todavía se está escribiendo, E la completa de golpe.
		if (_linea >= 0 && TextoDialogo.VisibleRatio < 1f)
		{
			_tweenTexto?.Kill();
			TextoDialogo.VisibleRatio = 1f;
			return;
		}

		_linea++;
		if (_linea >= Dialogo.Length)
		{
			Cerrar();
			return;
		}

		if (_linea == 0)
		{
			PanelDialogo.Visible = true;
			if (TextoNombre != null)
				TextoNombre.Text = NombreNpc;
			_npc?.EmpezarConversacion(_jugador);
			Enfocar(true);
		}
		MostrarLinea(Dialogo[_linea]);
	}

	private void MostrarLinea(string texto)
	{
		TextoDialogo.Text = texto;
		TextoDialogo.VisibleRatio = 0f;
		float duracion = texto.Length / VelocidadEscritura;

		// Si esta línea tiene grabación, suena completa y el texto la acompaña.
		_reproductorLinea.Stop();
		AudioStream grabacion = GrabacionDeLinea(_linea);
		if (grabacion != null)
		{
			_reproductorVoz.Stop();
			_reproductorLinea.Stream = grabacion;
			_reproductorLinea.Play();
			if (SincronizarTextoConVoz)
				duracion = Mathf.Max((float)grabacion.GetLength() * 0.9f, 0.5f);
		}

		_tweenTexto?.Kill();
		_tweenTexto = CreateTween();
		_tweenTexto.TweenProperty(TextoDialogo, "visible_ratio", 1f, duracion);
	}

	private void Cerrar()
	{
		bool estabaAbierto = _linea >= 0;
		_linea = -1;
		_tweenTexto?.Kill();
		_reproductorLinea?.Stop();
		if (PanelDialogo != null)
			PanelDialogo.Visible = false;
		_npc?.TerminarConversacion();
		if (estabaAbierto)
			Enfocar(false);
	}

	private void AlEntrar(Node2D cuerpo)
	{
		if (!cuerpo.IsInGroup("jugador"))
			return;
		_jugadorEnRango = true;
		_jugador = cuerpo;
	}

	private void AlSalir(Node2D cuerpo)
	{
		if (!cuerpo.IsInGroup("jugador"))
			return;
		_jugadorEnRango = false;
		Cerrar(); // Se cierra el diálogo si el jugador se aleja.
	}

	// ------------------------------------------------------------------
	// Voz: susurro continuo mientras se escribe el texto
	// ------------------------------------------------------------------

	private void PrepararVoz()
	{
		if (Voces.Length == 0)
		{
			// Primero los susurros nuevos (voz_ofelia_susurro_1.ogg ... _6.ogg, recortados de una
			// misma grabación); si no están, los susurros anteriores voz_ofelia_1.wav, _2 y _3.
			var encontradas = new System.Collections.Generic.List<AudioStream>();
			for (int i = 1; i <= 6; i++)
			{
				string ruta = $"res://assets/audio/voz_ofelia_susurro_{i}.ogg";
				if (ResourceLoader.Exists(ruta))
					encontradas.Add(GD.Load<AudioStream>(ruta));
			}
			if (encontradas.Count == 0)
			{
				for (int i = 1; i <= 3; i++)
				{
					string ruta = $"res://assets/audio/voz_ofelia_{i}.wav";
					if (ResourceLoader.Exists(ruta))
						encontradas.Add(GD.Load<AudioStream>(ruta));
				}
			}
			Voces = encontradas.ToArray();
		}
		_reproductorVoz = new AudioStreamPlayer { VolumeDb = VolumenVoz };
		AddChild(_reproductorVoz);

		if (VocesPorLinea.Length == 0)
		{
			var lineas = new AudioStream[Dialogo.Length];
			for (int i = 0; i < Dialogo.Length; i++)
			{
				foreach (string extension in new[] { "ogg", "wav", "mp3" })
				{
					string ruta = $"res://assets/audio/ofelia_linea_{i + 1}.{extension}";
					if (ResourceLoader.Exists(ruta))
					{
						lineas[i] = GD.Load<AudioStream>(ruta);
						break;
					}
				}
			}
			VocesPorLinea = lineas;
		}
		_reproductorLinea = new AudioStreamPlayer { VolumeDb = VolumenLineaDb };
		AddChild(_reproductorLinea);
	}

	private AudioStream GrabacionDeLinea(int linea)
	{
		return linea >= 0 && linea < VocesPorLinea.Length ? VocesPorLinea[linea] : null;
	}

	private void ActualizarVoz(float dt)
	{
		// Las líneas con grabación no usan los susurros.
		bool escribiendo = _linea >= 0 && TextoDialogo != null && TextoDialogo.VisibleRatio < 1f
			&& GrabacionDeLinea(_linea) == null;
		if (escribiendo)
		{
			// Encadena susurros mientras dure la línea.
			if (!_reproductorVoz.Playing && Voces.Length > 0)
			{
				_reproductorVoz.Stream = Voces[GD.RandRange(0, Voces.Length - 1)];
				_reproductorVoz.PitchScale = 1f + (float)GD.RandRange(-VariacionTono, VariacionTono);
				_reproductorVoz.VolumeDb = VolumenVoz;
				_reproductorVoz.Play();
			}
		}
		else if (_reproductorVoz.Playing)
		{
			// Terminó la línea: el susurro se apaga suave, no de golpe.
			_reproductorVoz.VolumeDb = Mathf.MoveToward(_reproductorVoz.VolumeDb, -40f, 80f * dt);
			if (_reproductorVoz.VolumeDb <= -39f)
				_reproductorVoz.Stop();
		}
	}

	// ------------------------------------------------------------------
	// Enfoque: cámara más cerca + luz sobre el NPC
	// ------------------------------------------------------------------

	private void CrearCapaLuz()
	{
		var capa = new CanvasLayer { Layer = 5 }; // sobre el nivel, debajo del HUD y del diálogo
		AddChild(capa);
		_materialLuz = new ShaderMaterial { Shader = new Shader { Code = ShaderLuz } };
		_capaLuz = new ColorRect { Material = _materialLuz, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_capaLuz.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		capa.AddChild(_capaLuz);
		_materialLuz.SetShaderParameter("radio", RadioLuz);
		_materialLuz.SetShaderParameter("oscuridad", 0f);
	}

	private void Enfocar(bool activar)
	{
		if (!EnfocarAlHablar)
			return;
		_tweenEnfoque?.Kill();
		_tweenEnfoque = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		// Oscuridad alrededor de la luz.
		_capaLuz.Visible = true;
		float desde = (float)_materialLuz.GetShaderParameter("oscuridad");
		_tweenEnfoque.TweenMethod(Callable.From<float>(v => _materialLuz.SetShaderParameter("oscuridad", v)),
			desde, activar ? OscuridadEnfoque : 0f, DuracionEnfoque);

		// Cámara: se acerca y se centra entre el Mago y el NPC.
		if (activar)
		{
			_camara = GetViewport().GetCamera2D();
			if (_camara != null)
			{
				_zoomOriginal = _camara.Zoom;
				_offsetOriginal = _camara.Offset;
			}
		}
		if (_camara != null && IsInstanceValid(_camara))
		{
			Vector2 zoom = activar ? _zoomOriginal * ZoomEnfoque : _zoomOriginal;
			Vector2 offset = _offsetOriginal;
			if (activar)
			{
				Vector2 centroCharla = (CentroNpc() + CentroJugador()) * 0.5f + new Vector2(0f, AjusteVertical);
				offset = OffsetPara(centroCharla, zoom);
			}
			_tweenEnfoque.TweenProperty(_camara, "zoom", zoom, DuracionEnfoque);
			_tweenEnfoque.TweenProperty(_camara, "offset", offset, DuracionEnfoque);
		}

		if (!activar)
			_tweenEnfoque.Chain().TweenCallback(Callable.From(() => { _capaLuz.Visible = false; }));
	}

	/// <summary>
	/// Calcula el offset para que la pantalla quede centrada en "objetivo" con el zoom dado.
	/// La cámara del Mago está limitada a los bordes del nivel: primero se calcula dónde
	/// la dejan esos límites con el nuevo zoom, y el objetivo también se limita para no
	/// mostrar nada fuera del nivel.
	/// </summary>
	private Vector2 OffsetPara(Vector2 objetivo, Vector2 zoom)
	{
		Vector2 mitad = GetViewport().GetVisibleRect().Size / zoom * 0.5f;
		Vector2 posicion = _camara.GlobalPosition;
		Vector2 baseCamara = new(
			Limitar(posicion.X, _camara.LimitLeft + mitad.X, _camara.LimitRight - mitad.X),
			Limitar(posicion.Y, _camara.LimitTop + mitad.Y, _camara.LimitBottom - mitad.Y));
		Vector2 destino = new(
			Limitar(objetivo.X, _camara.LimitLeft + mitad.X, _camara.LimitRight - mitad.X),
			Limitar(objetivo.Y, _camara.LimitTop + mitad.Y, _camara.LimitBottom - mitad.Y));
		return destino - baseCamara;
	}

	private static float Limitar(float valor, float minimo, float maximo)
	{
		return minimo > maximo ? (minimo + maximo) * 0.5f : Mathf.Clamp(valor, minimo, maximo);
	}

	/// <summary>Mueve la luz para que siga al NPC en pantalla (la cámara se está moviendo).</summary>
	private void ActualizarLuz()
	{
		if (!_capaLuz.Visible)
			return;
		Vector2 pantalla = GetViewport().GetVisibleRect().Size;
		Vector2 enPantalla = GetViewport().GetCanvasTransform() * CentroNpc();
		_materialLuz.SetShaderParameter("centro", enPantalla / pantalla);
		_materialLuz.SetShaderParameter("aspecto", pantalla.X / pantalla.Y);
		_materialLuz.SetShaderParameter("radio", RadioLuz);
	}

	private Vector2 CentroNpc()
	{
		var sprite = _npc?.GetNodeOrNull<Node2D>("Sprite");
		return sprite != null ? sprite.GlobalPosition : GlobalPosition + new Vector2(0f, -AlturaIndicador * 0.5f);
	}

	private Vector2 CentroJugador()
	{
		if (_jugador == null || !IsInstanceValid(_jugador))
			return CentroNpc();
		return _jugador.HasMethod("centro") ? _jugador.Call("centro").AsVector2() : _jugador.GlobalPosition;
	}

	// ------------------------------------------------------------------
	// Presentación
	// ------------------------------------------------------------------

	/// <summary>Indicador "E" flotando sobre el NPC mientras el jugador está cerca.</summary>
	public override void _Draw()
	{
		if (!_jugadorEnRango || _linea >= 0)
			return;
		var centro = new Vector2(0f, -AlturaIndicador + Mathf.Sin(_t * 4f) * 4f);
		DrawCircle(centro, 17f, Tinta);
		DrawArc(centro, 17f, 0f, Mathf.Tau, 32, Cian, 2.5f);
		DrawString(ThemeDB.FallbackFont, centro + new Vector2(-17f, 7f), "E",
			HorizontalAlignment.Center, 34f, 20, BlancoHueso);
	}

	/// <summary>Crea el recuadro con la cara dentro del panel. Devuelve el ancho que ocupa.</summary>
	private float CrearRetrato()
	{
		if (!MostrarRetrato)
			return 0f;
		Texture2D imagen = Retrato ?? CargarTextura("res://assets/personajes/ofelia_retrato.png") ?? RecortarCara();
		if (imagen == null)
			return 0f;

		float lado = AltoPanel - 16f;
		var marco = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
		var estilo = new StyleBoxFlat { BgColor = new Color(0.02f, 0.03f, 0.05f), BorderColor = Cian };
		estilo.SetBorderWidthAll(2);
		marco.AddThemeStyleboxOverride("panel", estilo);
		marco.Position = new Vector2(8f, 8f);
		marco.Size = new Vector2(lado, lado);
		marco.ClipContents = true;
		PanelDialogo.AddChild(marco);

		var cara = new TextureRect
		{
			Texture = imagen,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		cara.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		cara.OffsetLeft = 2f;
		cara.OffsetTop = 2f;
		cara.OffsetRight = -2f;
		cara.OffsetBottom = -2f;
		marco.AddChild(cara);
		return lado + 8f;
	}

	/// <summary>Sin retrato propio: recorta la parte de arriba (la cara) de la imagen del NPC.</summary>
	private Texture2D RecortarCara()
	{
		Texture2D origen = _npc?.ImagenDeFrente ?? _npc?.ImagenNormal;
		if (origen == null)
			return null;
		float alto = origen.GetHeight() * AltoCara;
		float ancho = Mathf.Min(origen.GetWidth(), alto);
		float x = (origen.GetWidth() - ancho) * 0.5f + origen.GetWidth() * CorrerRecorte;
		float y = origen.GetHeight() * BajarRecorte;
		x = Mathf.Clamp(x, 0f, origen.GetWidth() - ancho);
		return new AtlasTexture { Atlas = origen, Region = new Rect2(x, y, ancho, alto) };
	}

	private static Texture2D CargarTextura(string ruta)
	{
		return ResourceLoader.Exists(ruta) ? GD.Load<Texture2D>(ruta) : null;
	}

	private void AplicarFuentes()
	{
		FuenteNombre ??= CargarFuente("res://assets/fuentes/PirataOne-Regular.ttf");
		FuenteTexto ??= CargarFuente("res://assets/fuentes/IMFellEnglish-Regular.ttf");
		if (FuenteNombre != null && TextoNombre != null)
			TextoNombre.AddThemeFontOverride("font", FuenteNombre);
		if (FuenteTexto != null && TextoDialogo != null)
			TextoDialogo.AddThemeFontOverride("font", FuenteTexto);
	}

	private static Font CargarFuente(string ruta)
	{
		return ResourceLoader.Exists(ruta) ? GD.Load<Font>(ruta) : null;
	}

	/// <summary>Panel con fondo oscuro y borde cian, nombre arriba y texto debajo.</summary>
	private void Estilizar()
	{
		var estilo = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.04f, 0.04f, 0.92f),
			BorderColor = Cian,
		};
		estilo.SetBorderWidthAll(3);
		estilo.SetCornerRadiusAll(4);
		PanelDialogo.AddThemeStyleboxOverride("panel", estilo);

		PanelDialogo.SetAnchorsPreset(Arriba ? Control.LayoutPreset.TopWide : Control.LayoutPreset.BottomWide);
		PanelDialogo.OffsetLeft = MargenCostados;
		PanelDialogo.OffsetRight = -MargenCostados;
		if (Arriba)
		{
			PanelDialogo.OffsetTop = MargenBorde;
			PanelDialogo.OffsetBottom = MargenBorde + AltoPanel;
		}
		else
		{
			PanelDialogo.OffsetTop = -(AltoPanel + MargenBorde);
			PanelDialogo.OffsetBottom = -MargenBorde;
		}

		float altoNombre = TamanioNombre + 10f;
		if (TextoNombre != null)
		{
			TextoNombre.SetAnchorsPreset(Control.LayoutPreset.TopWide);
			TextoNombre.OffsetLeft = 18f;
			TextoNombre.OffsetRight = -18f;
			TextoNombre.OffsetTop = 8f;
			TextoNombre.OffsetBottom = 8f + altoNombre;
			TextoNombre.AddThemeColorOverride("font_color", Cian);
			TextoNombre.AddThemeFontSizeOverride("font_size", TamanioNombre);
		}

		float margenTexto = 18f + CrearRetrato();
		if (TextoNombre != null)
			TextoNombre.OffsetLeft = margenTexto;

		TextoDialogo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		TextoDialogo.OffsetLeft = margenTexto;
		TextoDialogo.OffsetRight = -18f;
		TextoDialogo.OffsetTop = TextoNombre != null ? 12f + altoNombre : 12f;
		TextoDialogo.OffsetBottom = -10f;
		TextoDialogo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		TextoDialogo.AddThemeColorOverride("font_color", BlancoHueso);
		TextoDialogo.AddThemeFontSizeOverride("font_size", TamanioTexto);
	}
}
