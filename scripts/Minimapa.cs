using Godot;
using System.Collections.Generic;

/// <summary>
/// Mapa del Circo, con estética de cartel de feria maldito.
/// Mientras se juega solo se ve un ícono chico (un boleto con una calavera) arriba a la derecha.
/// Al apretar M se abre el mapa y el juego se pausa: un cartel enmarcado con focos de feria
/// (algunos quemados y otros que titilan), con las cinco zonas como boletos de entrada unidos
/// por cadenas. La zona actual brilla en rojo; las que todavía no se alcanzaron están
/// encadenadas y con candado ("???"). Abajo, la zona actual y sus objetivos, que se tildan
/// solos según la partida guardada. Se cierra con M o Esc.
///
/// Uso: agregar un nodo Control como hijo del HUD (CanvasLayer) y asignarle este script.
/// En cada nivel, poner en ZonaActual el número de zona (Nivel 1 = 0).
/// </summary>
public partial class Minimapa : Control
{
	/// <summary>0 = Entrada, 1 = Jaulas, 2 = Camerinos, 3 = Arena Central, 4 = Corazón Maldito.</summary>
	[Export(PropertyHint.Range, "0,4")] public int ZonaActual = 0;
	/// <summary>Muestra nombre y jefe de todas las zonas aunque no se haya llegado.</summary>
	[Export] public bool MostrarTodo = false;
	[Export] public bool PausarAlAbrir = true;
	[Export] public Key TeclaMapa = Key.M;
	[Export] public float MargenDerecho = 24f;
	[Export] public float MargenSuperior = 24f;

	private static readonly string[] Numeros = { "I", "II", "III", "IV", "V" };
	private static readonly string[] Nombres =
		{ "La Entrada del Circo", "Las Jaulas", "Los Camerinos", "La Arena Central", "El Corazón Maldito" };
	private static readonly string[] Jefes =
		{ "Domador Corrompido", "Bailarina Espectral", "Payaso Devorador", "Guardianes de Arena", "El Director" };
	private static readonly string[] Recompensas =
		{ "Hechizo de Fuego", "Hechizo de Hielo", "Hechizo de Rayo", "Paso al Corazón Maldito", "Purificar el circo" };
	/// <summary>Inclinación de cada boleto, para que parezcan pegados a mano.</summary>
	private static readonly float[] Inclinaciones = { -4f, 3f, -2f, 4f, -3f };

	// Paleta del GDD.
	private static readonly Color Hueso = new("F5F2EA");
	private static readonly Color Pergamino = new("D2C4A7");
	private static readonly Color PergaminoGastado = new("9C9287");
	private static readonly Color PapelOscuro = new("4A4440");
	private static readonly Color PapelNegro = new("221E1E");
	private static readonly Color Tinta = new("0E0B0B");
	private static readonly Color Carmesi = new("9C171F");
	private static readonly Color Granate = new("5C1D1C");
	private static readonly Color Ardiente = new("E33224");
	private static readonly Color Tenue = new(0.82f, 0.77f, 0.65f, 0.7f);
	private static readonly Color RayaClara = new(0.094f, 0.047f, 0.047f, 0.98f);
	private static readonly Color RayaOscura = new(0.067f, 0.039f, 0.039f, 0.98f);
	private static readonly Color Hierro = new("6E6E76");
	private static readonly Color HierroOscuro = new("46464C");
	private static readonly Color Foco = new("FFC247");
	private static readonly Color FocoQuemado = new("3C2819");
	private static readonly Color Bastidor = new("2A1A12");

	private enum Estado { Completada, Actual, Siguiente, Desconocida }

	private const float AnchoBoleto = 124f;
	private const float AltoBoleto = 72f;

	private bool _abierto;
	private float _t;
	private Node _partida;
	private Font _fuenteTitulo;
	private Font _fuenteTexto;
	private Font _fuenteVersalitas;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		// Sigue funcionando con el juego pausado, para poder cerrar el mapa.
		ProcessMode = ProcessModeEnum.Always;
		_partida = GetNodeOrNull("/root/Partida");
		_fuenteTitulo = CargarFuente("res://assets/fuentes/PirataOne-Regular.ttf");
		_fuenteTexto = CargarFuente("res://assets/fuentes/IMFellEnglish-Regular.ttf");
		_fuenteVersalitas = CargarFuente("res://assets/fuentes/IMFellEnglishSC-Regular.ttf");
	}

	private static Font CargarFuente(string ruta) =>
		ResourceLoader.Exists(ruta) ? GD.Load<Font>(ruta) : ThemeDB.FallbackFont;

	public override void _Input(InputEvent evento)
	{
		if (evento is not InputEventKey tecla || !tecla.Pressed || tecla.Echo)
			return;
		if (tecla.PhysicalKeycode == TeclaMapa || (_abierto && tecla.PhysicalKeycode == Key.Escape))
		{
			_abierto = !_abierto;
			if (PausarAlAbrir)
				GetTree().Paused = _abierto;
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _ExitTree()
	{
		// Si se cambia de escena con el mapa abierto, que el juego no quede pausado.
		if (_abierto && PausarAlAbrir)
			GetTree().Paused = false;
	}

	public override void _Process(double delta)
	{
		_t += (float)delta;
		Position = Vector2.Zero;
		Size = GetViewportRect().Size;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_abierto)
			DibujarMapa();
		else
			DibujarIcono();
	}

	// ------------------------------------------------------------------
	// Ícono (lo único que se ve mientras se juega)
	// ------------------------------------------------------------------

	private void DibujarIcono()
	{
		Vector2 tam = new(60, 36);
		Vector2 centro = new(Size.X - MargenDerecho - tam.X / 2f, MargenSuperior + tam.Y / 2f);
		DrawSetTransform(centro, Mathf.DegToRad(-5f));

		float brillo = 0.18f + 0.1f * Mathf.Sin(_t * 2.5f);
		DrawColoredPolygon(FormaBoleto(tam.X + 10f, tam.Y + 10f, 7f), new Color(Ardiente, brillo));
		Vector2[] forma = FormaBoleto(tam.X, tam.Y, 5f);
		DrawColoredPolygon(forma, Pergamino);
		DibujarContorno(forma, Tinta, 1.5f);
		LineaPunteadaVertical(tam.X * 0.18f, tam.Y, new Color(Tinta, 0.6f));

		// La tecla M en el cuerpo del boleto y la calavera en el talón.
		DrawString(_fuenteTitulo, new Vector2(-tam.X / 2f, 9f), "M",
			HorizontalAlignment.Center, tam.X * 0.68f, 24, Carmesi);
		Calavera(new Vector2(tam.X * 0.34f, 0f), 0.75f, Carmesi, Pergamino);
		DrawSetTransform(Vector2.Zero, 0f);
	}

	// ------------------------------------------------------------------
	// Mapa abierto
	// ------------------------------------------------------------------

	private Estado EstadoDe(int zona)
	{
		if (zona < ZonaActual) return Estado.Completada;
		if (zona == ZonaActual) return Estado.Actual;
		if (zona == ZonaActual + 1) return Estado.Siguiente;
		return Estado.Desconocida;
	}

	private bool NombreVisible(int zona) => MostrarTodo || zona <= ZonaActual + 1;
	private bool DetalleVisible(int zona) => MostrarTodo || zona <= ZonaActual;

	private bool Progreso(string propiedad) =>
		_partida != null && _partida.Get(propiedad).AsBool();

	private void DibujarMapa()
	{
		// Velo oscuro con viñeta roja.
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.72f));
		for (int k = 0; k < 30; k++)
		{
			float a = 0.16f * (1f - k / 30f);
			DrawRect(new Rect2(k * 4f, k * 4f, Size.X - k * 8f, Size.Y - k * 8f), new Color(0.43f, 0.04f, 0.05f, a), false, 4f);
		}

		Vector2 tam = new(Mathf.Min(900f, Size.X - 60f), Mathf.Min(520f, Size.Y - 60f));
		var panel = new Rect2(((Size - tam) / 2f).Floor(), tam);
		Vector2 o = panel.Position;
		float w = tam.X;

		// Lona a rayas, oscurecida en los bordes.
		for (float x = 0; x < w; x += 36f)
		{
			DrawRect(new Rect2(o.X + x, o.Y, Mathf.Min(18f, w - x), tam.Y), RayaClara);
			if (x + 18f < w)
				DrawRect(new Rect2(o.X + x + 18f, o.Y, Mathf.Min(18f, w - x - 18f), tam.Y), RayaOscura);
		}
		for (int k = 0; k < 18; k++)
		{
			float a = 0.24f * (1f - k / 18f);
			DrawRect(new Rect2(o.X + k * 3f, o.Y + k * 3f, w - k * 6f, tam.Y - k * 6f), new Color(0, 0, 0, a), false, 3f);
		}

		DibujarTitulo(o, w);

		// Boletos unidos por cadenas.
		float margen = w * 0.11f;
		float paso = (w - 2f * margen) / (Nombres.Length - 1);
		var posiciones = new Vector2[Nombres.Length];
		for (int i = 0; i < Nombres.Length; i++)
			posiciones[i] = o + new Vector2(margen + paso * i, 175f + (i % 2 == 1 ? 8f : -6f));
		for (int i = 0; i < Nombres.Length - 1; i++)
			Cadena(posiciones[i], posiciones[i + 1], i < ZonaActual ? Hierro : new Color(HierroOscuro, 0.8f));
		for (int i = 0; i < Nombres.Length; i++)
			Boleto(posiciones[i], i);

		// Nombres debajo de cada boleto.
		for (int i = 0; i < Nombres.Length; i++)
		{
			Vector2 p = posiciones[i];
			float ancho = paso - 6f;
			Estado estado = EstadoDe(i);
			DrawString(_fuenteTexto, new Vector2(p.X - ancho / 2f, p.Y + 60f), NombreVisible(i) ? Nombres[i] : "???",
				HorizontalAlignment.Center, ancho, 17, estado == Estado.Actual ? Hueso : Tenue);
			DrawString(_fuenteTexto, new Vector2(p.X - ancho / 2f, p.Y + 80f), DetalleVisible(i) ? Jefes[i] : "Jefe: ???",
				HorizontalAlignment.Center, ancho, 14, Tenue);
			if (estado == Estado.Actual)
			{
				float anchoTexto = _fuenteVersalitas.GetStringSize("estás acá", HorizontalAlignment.Left, -1, 13).X;
				DrawString(_fuenteVersalitas, new Vector2(p.X - ancho / 2f + 6f, p.Y + 101f), "estás acá",
					HorizontalAlignment.Center, ancho, 13, Ardiente);
				float tx = p.X + 6f - anchoTexto / 2f - 10f;
				float salto = 2f * Mathf.Sin(_t * 5f);
				DrawColoredPolygon(new[]
				{
					new Vector2(tx, p.Y + 91f + salto), new Vector2(tx - 5f, p.Y + 99f + salto), new Vector2(tx + 5f, p.Y + 99f + salto),
				}, Ardiente);
			}
		}

		DibujarDetalle(o, w);

		DrawString(_fuenteVersalitas, new Vector2(o.X, panel.End.Y - 22f), "M o Esc: cerrar",
			HorizontalAlignment.Center, w, 14, Tenue);
		DibujarMarquesina(panel);
	}

	private void DibujarTitulo(Vector2 o, float w)
	{
		const string titulo = "El Circo Maldito";
		const int tamanio = 46;
		float y = o.Y + 60f;
		DrawString(_fuenteTitulo, new Vector2(o.X + 2f, y + 3f), titulo, HorizontalAlignment.Center, w, tamanio, Carmesi);
		DrawString(_fuenteTitulo, new Vector2(o.X, y), titulo, HorizontalAlignment.Center, w, tamanio, Hueso);

		// Gotas de sangre que chorrean del título (se estiran muy despacio).
		float anchoTitulo = _fuenteTitulo.GetStringSize(titulo, HorizontalAlignment.Left, -1, tamanio).X;
		float x0 = o.X + w / 2f - anchoTitulo / 2f;
		float[] lugares = { 0.12f, 0.31f, 0.47f, 0.66f, 0.83f, 0.93f };
		float[] largos = { 10f, 18f, 7f, 14f, 22f, 9f };
		for (int i = 0; i < lugares.Length; i++)
		{
			float x = x0 + anchoTitulo * lugares[i];
			float largo = largos[i] + 2.5f * Mathf.Sin(_t * 0.7f + i * 1.7f);
			DrawRect(new Rect2(x - 1.5f, y + 4f, 3f, largo), Carmesi);
			DrawCircle(new Vector2(x, y + 4f + largo), 3f, Carmesi);
		}

		DrawString(_fuenteVersalitas, new Vector2(o.X, o.Y + 102f), "— mapa de la carpa —",
			HorizontalAlignment.Center, w, 15, Tenue);
	}

	private void DibujarDetalle(Vector2 o, float w)
	{
		float y = o.Y + 332f;
		DrawLine(new Vector2(o.X + 60f, y - 30f), new Vector2(o.X + w - 60f, y - 30f), new Color(Carmesi, 0.47f), 1f);

		// Izquierda: dónde estás.
		float xIzq = o.X + 60f;
		float anchoCol = w / 2f - 84f;
		DrawString(_fuenteVersalitas, new Vector2(xIzq, y), "Estás en", HorizontalAlignment.Left, anchoCol, 15, Ardiente);
		DrawString(_fuenteTitulo, new Vector2(xIzq, y + 38f), Nombres[ZonaActual], HorizontalAlignment.Left, anchoCol, 34, Hueso);
		DrawString(_fuenteTexto, new Vector2(xIzq, y + 70f), $"Jefe: {Jefes[ZonaActual]}", HorizontalAlignment.Left, anchoCol, 18, Hueso);
		DrawString(_fuenteTexto, new Vector2(xIzq, y + 96f), $"Recompensa: {Recompensas[ZonaActual]}", HorizontalAlignment.Left, anchoCol, 18, Hueso);
		if (ZonaActual + 1 < Nombres.Length)
			DrawString(_fuenteTexto, new Vector2(xIzq, y + 130f), $"Después: {Nombres[ZonaActual + 1]}",
				HorizontalAlignment.Left, anchoCol, 16, Tenue);

		// Derecha: objetivos con sellos de lacre.
		float xDer = o.X + w / 2f + 30f;
		DrawLine(new Vector2(o.X + w / 2f, y - 12f), new Vector2(o.X + w / 2f, y + 138f), new Color(Carmesi, 0.47f), 1f);
		DrawString(_fuenteVersalitas, new Vector2(xDer, y), "Objetivos", HorizontalAlignment.Left, anchoCol, 15, Ardiente);
		float yObj = y + 36f;
		foreach (var (texto, hecho) in Objetivos())
		{
			Vector2 sello = new(xDer + 8f, yObj - 6f);
			if (hecho)
			{
				DrawCircle(sello, 9f, Carmesi);
				DrawArc(sello, 9f, 0f, Mathf.Tau, 20, Tinta, 1.5f);
				DrawPolyline(new[] { sello + new Vector2(-4, 0), sello + new Vector2(-1, 4), sello + new Vector2(5, -4) }, Hueso, 2f);
			}
			else
			{
				DrawArc(sello, 8f, 0f, Mathf.Tau, 20, Tenue, 1f);
			}
			DrawString(_fuenteTexto, new Vector2(xDer + 28f, yObj), texto, HorizontalAlignment.Left, anchoCol - 28f, 18,
				hecho ? Hueso : Tenue);
			yObj += 31f;
		}
	}

	/// <summary>Objetivos de la zona actual y si ya se cumplieron (por ahora, los del Nivel 1).</summary>
	private IEnumerable<(string, bool)> Objetivos()
	{
		if (ZonaActual != 0)
			yield break;
		yield return ("Encender el Santuario del Farol", Progreso("hay_checkpoint"));
		yield return ("Purificar al Domador Corrompido", Progreso("domador_derrotado"));
		yield return ("Obtener el Hechizo de Fuego", Progreso("tiene_fuego"));
		yield return ("Quemar la barricada de telones", Progreso("barricada_quemada"));
	}

	// ------------------------------------------------------------------
	// Piezas
	// ------------------------------------------------------------------

	/// <summary>Rectángulo con las esquinas cortadas, centrado en el origen.</summary>
	private static Vector2[] FormaBoleto(float ancho, float alto, float corte)
	{
		float x = ancho / 2f, y = alto / 2f;
		return new[]
		{
			new Vector2(-x + corte, -y), new Vector2(x - corte, -y), new Vector2(x, -y + corte), new Vector2(x, y - corte),
			new Vector2(x - corte, y), new Vector2(-x + corte, y), new Vector2(-x, y - corte), new Vector2(-x, -y + corte),
		};
	}

	private void DibujarContorno(Vector2[] puntos, Color color, float grosor)
	{
		var cerrado = new Vector2[puntos.Length + 1];
		puntos.CopyTo(cerrado, 0);
		cerrado[^1] = puntos[0];
		DrawPolyline(cerrado, color, grosor);
	}

	private void LineaPunteadaVertical(float x, float alto, Color color)
	{
		for (float y = -alto / 2f + 2f; y < alto / 2f - 2f; y += 8f)
			DrawLine(new Vector2(x, y), new Vector2(x, Mathf.Min(y + 3f, alto / 2f - 2f)), color, 1.5f);
	}

	private void Boleto(Vector2 centro, int zona)
	{
		Estado estado = EstadoDe(zona);
		DrawSetTransform(centro, Mathf.DegToRad(Inclinaciones[zona]));
		const float w = AnchoBoleto, h = AltoBoleto;

		if (estado == Estado.Actual)
		{
			// Resplandor rojo que late.
			float pulso = 0.5f + 0.5f * Mathf.Sin(_t * 3.5f);
			for (int k = 4; k >= 1; k--)
				DrawColoredPolygon(FormaBoleto(w + k * 6f, h + k * 6f, 9f + k * 3f), new Color(Ardiente, (0.06f + 0.05f * pulso) * (5 - k) / 2f));
		}

		Color papel = estado switch
		{
			Estado.Actual => Pergamino,
			Estado.Completada => PergaminoGastado,
			Estado.Siguiente => PapelOscuro,
			_ => PapelNegro,
		};
		Vector2[] forma = FormaBoleto(w, h, 9f);
		DrawColoredPolygon(forma, papel);
		DibujarContorno(forma, Tinta, 2f);
		LineaPunteadaVertical(w * 0.18f, h, new Color(Tinta, 0.6f));

		bool clara = estado is Estado.Actual or Estado.Completada;
		Color tinta = estado == Estado.Actual ? Carmesi : clara ? Tinta : Tenue;
		float xCuerpo = -w / 2f;
		float anchoCuerpo = w * 0.68f;
		DrawString(_fuenteVersalitas, new Vector2(xCuerpo, -h / 2f + 22f), "Zona", HorizontalAlignment.Center, anchoCuerpo, 12,
			estado == Estado.Actual ? Granate : tinta);
		bool oculta = estado == Estado.Desconocida && !MostrarTodo;
		DrawString(_fuenteTitulo, new Vector2(xCuerpo, -h / 2f + 58f), oculta ? "?" : Numeros[zona],
			HorizontalAlignment.Center, anchoCuerpo, 34, tinta);

		// Talón: calavera si ya se llegó, candado si no.
		Vector2 talon = new(w * 0.34f, 0f);
		if (clara)
			Calavera(talon, 1f, estado == Estado.Actual ? Carmesi : Granate, papel);
		else
			Candado(talon, estado == Estado.Siguiente ? Tenue : new Color("6A625A"));

		if (estado == Estado.Desconocida && !MostrarTodo)
		{
			// Encadenado.
			Vector2 a1 = new(-w / 2f, -h / 2f + 10f), b1 = new(w / 2f, h / 2f - 10f);
			Vector2 a2 = new(-w / 2f, h / 2f - 10f), b2 = new(w / 2f, -h / 2f + 10f);
			DrawLine(a1, b1, Hierro, 4f);
			DrawLine(a1, b1, new Color("28282C"), 1.5f);
			DrawLine(a2, b2, Hierro, 4f);
			DrawLine(a2, b2, new Color("28282C"), 1.5f);
		}
		if (estado == Estado.Completada)
			DrawString(_fuenteVersalitas, new Vector2(-w / 2f, 8f), "purificada", HorizontalAlignment.Center, w * 0.8f, 16,
				new Color(Carmesi, 0.8f));

		DrawSetTransform(Vector2.Zero, 0f);
	}

	private void Calavera(Vector2 c, float escala, Color color, Color fondo)
	{
		float s = escala;
		DrawCircle(c + new Vector2(0, -3f * s), 10f * s, color);
		DrawRect(new Rect2(c + new Vector2(-6f * s, 2f * s), new Vector2(12f * s, 9f * s)), color);
		DrawCircle(c + new Vector2(-4f * s, -3f * s), 3f * s, fondo);
		DrawCircle(c + new Vector2(4f * s, -3f * s), 3f * s, fondo);
		DrawColoredPolygon(new[] { c + new Vector2(0, 1f * s), c + new Vector2(-2f * s, 5f * s), c + new Vector2(2f * s, 5f * s) }, fondo);
		for (int i = -1; i <= 1; i++)
			DrawLine(c + new Vector2(3f * i * s, 7f * s), c + new Vector2(3f * i * s, 11f * s), fondo, Mathf.Max(1f, s));
	}

	private void Candado(Vector2 c, Color color)
	{
		DrawArc(c + new Vector2(0, -8f), 8f, Mathf.Pi, Mathf.Tau, 12, color, 3f);
		var cuerpo = new Rect2(c + new Vector2(-11f, -8f), new Vector2(22f, 18f));
		DrawRect(cuerpo, color);
		DrawRect(cuerpo, Tinta, false, 1.5f);
		DrawCircle(c + new Vector2(0, 0.5f), 2.5f, Tinta);
		DrawLine(c + new Vector2(0, 1f), c + new Vector2(0, 5f), Tinta, 1.5f);
	}

	private void Cadena(Vector2 a, Vector2 b, Color color)
	{
		float largo = a.DistanceTo(b);
		int eslabones = Mathf.Max(2, (int)(largo / 11f));
		float angulo = (b - a).Angle();
		for (int k = 0; k < eslabones; k++)
		{
			Vector2 p = a.Lerp(b, (k + 0.5f) / eslabones);
			DrawSetTransform(p, angulo);
			if (k % 2 == 0)
				DrawRect(new Rect2(-7f, -3.5f, 14f, 7f), color, false, 2f);
			else
				DrawLine(new Vector2(-6f, 0), new Vector2(6f, 0), color, 3f);
		}
		DrawSetTransform(Vector2.Zero, 0f);
	}

	/// <summary>Marco de madera con focos de feria: la mayoría titilan suave, algunos parpadean y otros están quemados.</summary>
	private void DibujarMarquesina(Rect2 panel)
	{
		DrawRect(panel, Bastidor, false, 10f);
		DrawRect(panel.Grow(5f), Tinta, false, 2f);
		DrawRect(panel.Grow(-5f), Tinta, false, 2f);

		var focos = new List<Vector2>();
		const float paso = 30f;
		for (float x = 0; x <= panel.Size.X + 0.1f; x += paso)
		{
			focos.Add(panel.Position + new Vector2(x, 0));
			focos.Add(panel.Position + new Vector2(x, panel.Size.Y));
		}
		for (float y = paso; y < panel.Size.Y; y += paso)
		{
			focos.Add(panel.Position + new Vector2(0, y));
			focos.Add(panel.Position + new Vector2(panel.Size.X, y));
		}

		for (int k = 0; k < focos.Count; k++)
		{
			Vector2 p = focos[k];
			float brillo;
			if (k % 9 == 4)
				brillo = 0f;                                                     // quemado
			else if (k % 13 == 7)
				brillo = Mathf.Sin(_t * 23f + k) > 0.2f ? 1f : 0.15f;          // falla
			else
				brillo = 0.75f + 0.25f * Mathf.Sin(_t * 3f - k * 0.45f);         // corre la luz

			if (brillo > 0f)
			{
				DrawCircle(p, 13f, new Color(1f, 0.67f, 0.24f, 0.07f * brillo));
				DrawCircle(p, 9f, new Color(1f, 0.67f, 0.24f, 0.14f * brillo));
				DrawCircle(p, 6.5f, new Color(1f, 0.7f, 0.3f, 0.25f * brillo));
			}
			DrawCircle(p, 4.5f, brillo > 0f ? FocoQuemado.Lerp(Foco, brillo) : FocoQuemado);
			DrawArc(p, 4.5f, 0f, Mathf.Tau, 12, Tinta, 1f);
		}
	}
}
