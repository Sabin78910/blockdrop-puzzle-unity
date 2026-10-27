using System;
using System.Collections.Generic;
using System.Globalization;

namespace BlockDrop.Core
{
    public enum Lang { En, Es, Pt, Ne }

    /// <summary>English text is the key. Spanish (Latin America) and Portuguese (Brazil) first: Brazil and Mexico
    /// are the top growth markets for block puzzles. Missing keys fall back to English.</summary>
    public static class Loc
    {
        public static Lang Current = Lang.En;

        public static readonly string[] Names = { "ENGLISH", "ESPAÑOL", "PORTUGUÊS", "नेपाली" };

        public static Lang FromCode(string code)
        {
            code = (code ?? "").ToLowerInvariant();
            return code.StartsWith("es") ? Lang.Es : code.StartsWith("pt") ? Lang.Pt : code.StartsWith("ne") ? Lang.Ne : Lang.En;
        }

        public static string Code(Lang l) => l == Lang.Es ? "es" : l == Lang.Pt ? "pt" : l == Lang.Ne ? "ne" : "en";

        public static string L(string en)
        {
            if (Current == Lang.En || string.IsNullOrEmpty(en)) return en;
            if (Current == Lang.Ne) return Nepali.Table.TryGetValue(en, out var n) ? n : en;
            if (!Table.TryGetValue(en, out var t)) return en;
            return Current == Lang.Es ? t.es : t.pt;
        }

        public static string F(string template, params object[] args) => string.Format(CultureInfo.InvariantCulture, L(template), args);

        public static readonly Dictionary<string, (string es, string pt)> Table = new Dictionary<string, (string, string)>
        {
            // menu and navigation
            ["PLAY"] = ("JUGAR", "JOGAR"), ["DAILY"] = ("DIARIO", "DIÁRIO"), ["VS AI"] = ("VS IA", "VS IA"),
            ["2 PLAYERS"] = ("2 JUGADORES", "2 JOGADORES"), ["CHALLENGE"] = ("DESAFÍO", "DESAFIO"), ["RANKINGS"] = ("RANKING", "RANKING"),
            ["MISSIONS"] = ("MISIONES", "MISSÕES"), ["THEMES"] = ("TEMAS", "TEMAS"), ["TROPHIES"] = ("TROFEOS", "TROFÉUS"),
            ["LEVELS"] = ("NIVELES", "NÍVEIS"), ["BACK"] = ("VOLVER", "VOLTAR"), ["MENU"] = ("MENÚ", "MENU"),
            ["SOUND: ON"] = ("SONIDO: SÍ", "SOM: LIGADO"), ["SOUND: OFF"] = ("SONIDO: NO", "SOM: DESLIGADO"),
            ["VIBRATE: ON"] = ("VIBRAR: SÍ", "VIBRAR: SIM"), ["VIBRATE: OFF"] = ("VIBRAR: NO", "VIBRAR: NÃO"),
            ["ONLINE"] = ("EN LÍNEA", "ONLINE"), ["OFFLINE"] = ("SIN CONEXIÓN", "OFFLINE"),
            ["Set your name"] = ("Elige tu nombre", "Escolha seu nome"), ["COLLECT"] = ("RECOGER", "COLETAR"),
            // game
            ["Drag a piece onto the board"] = ("Arrastra una pieza al tablero", "Arraste uma peça para o tabuleiro"),
            ["Fill a row or column to clear it!"] = ("¡Llena una fila o columna para borrarla!", "Complete uma linha ou coluna para limpá-la!"),
            ["Clear again and again for COMBOS!"] = ("¡Borra seguido para hacer COMBOS!", "Limpe seguido para fazer COMBOS!"),
            ["GREAT!"] = ("¡GENIAL!", "ÓTIMO!"), ["NICE!"] = ("¡BIEN!", "BOA!"), ["EXCELLENT!"] = ("¡EXCELENTE!", "EXCELENTE!"),
            ["AMAZING!"] = ("¡INCREÍBLE!", "INCRÍVEL!"), ["UNBELIEVABLE!"] = ("¡INCREÍBLE!", "INACREDITÁVEL!"),
            ["PERFECT CLEAR!"] = ("¡TABLERO LIMPIO!", "TABULEIRO LIMPO!"), ["NEW BEST!"] = ("¡NUEVO RÉCORD!", "NOVO RECORDE!"),
            ["MISSION COMPLETE!"] = ("¡MISIÓN CUMPLIDA!", "MISSÃO CUMPRIDA!"), ["SECOND CHANCE!"] = ("¡SEGUNDA OPORTUNIDAD!", "SEGUNDA CHANCE!"),
            ["OUT OF MOVES"] = ("SIN MOVIMIENTOS", "SEM JOGADAS"), ["CONTINUE  (FREE)"] = ("CONTINUAR  (GRATIS)", "CONTINUAR  (GRÁTIS)"),
            ["WATCH VIDEO TO CONTINUE"] = ("VER VIDEO Y CONTINUAR", "VER VÍDEO E CONTINUAR"), ["NO THANKS"] = ("NO, GRACIAS", "NÃO, OBRIGADO"),
            ["PAUSED"] = ("PAUSA", "PAUSADO"), ["RESUME"] = ("SEGUIR", "CONTINUAR"), ["QUIT TO MENU"] = ("SALIR AL MENÚ", "SAIR PARA O MENU"),
            ["Quitting ends this game."] = ("Salir termina esta partida.", "Sair encerra esta partida."),
            ["GAME OVER"] = ("FIN DEL JUEGO", "FIM DE JOGO"), ["PLAY AGAIN"] = ("JUGAR DE NUEVO", "JOGAR DE NOVO"),
            ["LEVEL COMPLETE!"] = ("¡NIVEL COMPLETADO!", "NÍVEL CONCLUÍDO!"), ["NEXT LEVEL"] = ("SIGUIENTE NIVEL", "PRÓXIMO NÍVEL"),
            ["RETRY"] = ("REINTENTAR", "TENTAR DE NOVO"), ["REMATCH"] = ("REVANCHA", "REVANCHE"),
            ["YOU"] = ("TÚ", "VOCÊ"), ["YOU WIN!"] = ("¡GANASTE!", "VOCÊ VENCEU!"), ["AI WINS"] = ("GANA LA IA", "A IA VENCEU"),
            ["IT'S A TIE!"] = ("¡EMPATE!", "EMPATE!"), ["AI is thinking"] = ("La IA está pensando", "A IA está pensando"),
            ["AI +line"] = ("IA +línea", "IA +linha"), ["(out)"] = ("(fuera)", "(fora)"), ["(you)"] = ("(tú)", "(você)"),
            ["GREAT SESSION!"] = ("¡GRAN SESIÓN!", "ÓTIMA SESSÃO!"), ["KEEP PLAYING"] = ("SEGUIR JUGANDO", "CONTINUAR JOGANDO"),
            ["You've played for 40 minutes. A quick stretch keeps your mind sharp."] = ("Llevas 40 minutos jugando. Estírate un poco para mantener la mente ágil.", "Você jogou por 40 minutos. Alongue-se um pouco para manter a mente afiada."),
            // vs ai
            ["Same pieces for you and the AI. Highest score wins."] = ("Las mismas piezas para ti y la IA. Gana el mayor puntaje.", "As mesmas peças para você e a IA. Vence a maior pontuação."),
            ["EASY"] = ("FÁCIL", "FÁCIL"), ["MEDIUM"] = ("MEDIO", "MÉDIO"), ["HARD (MASTER AI)"] = ("DIFÍCIL (IA MAESTRA)", "DIFÍCIL (IA MESTRE)"),
            ["Easy"] = ("Fácil", "Fácil"), ["Medium"] = ("Medio", "Médio"), ["Hard"] = ("Difícil", "Difícil"),
            // challenge
            ["Start a challenge and share its code, or enter a friend's code to play their exact pieces."] = ("Crea un desafío y comparte su código, o escribe el código de un amigo para jugar con sus mismas piezas.", "Crie um desafio e compartilhe o código, ou digite o código de um amigo para jogar com as mesmas peças."),
            ["NEW CHALLENGE"] = ("NUEVO DESAFÍO", "NOVO DESAFIO"), ["FRIEND'S CODE"] = ("CÓDIGO DE AMIGO", "CÓDIGO DO AMIGO"),
            ["PLAY CODE"] = ("JUGAR CÓDIGO", "JOGAR CÓDIGO"), ["COPY CODE"] = ("COPIAR CÓDIGO", "COPIAR CÓDIGO"),
            ["Send this code to a friend. They get exactly your pieces. Highest score wins!"] = ("Envía este código a un amigo. Tendrá exactamente tus piezas. ¡Gana el mayor puntaje!", "Envie este código para um amigo. Ele terá exatamente as suas peças. Vence a maior pontuação!"),
            // leaderboard and profile
            ["LEADERBOARD"] = ("CLASIFICACIÓN", "CLASSIFICAÇÃO"), ["CLASSIC"] = ("CLÁSICO", "CLÁSSICO"), ["Loading…"] = ("Cargando…", "Carregando…"),
            ["No scores yet.\nBe the first!"] = ("Aún no hay puntajes.\n¡Sé el primero!", "Ainda não há pontuações.\nSeja o primeiro!"),
            ["Leaderboard unavailable right now.\nPlease try again later."] = ("Clasificación no disponible.\nInténtalo más tarde.", "Classificação indisponível.\nTente novamente mais tarde."),
            ["Offline — playing without leaderboards"] = ("Sin conexión: jugando sin clasificación", "Offline: jogando sem classificação"),
            ["Connecting…"] = ("Conectando…", "Conectando…"), ["Online"] = ("En línea", "Online"),
            ["YOUR NAME"] = ("TU NOMBRE", "SEU NOME"), ["Your name"] = ("Tu nombre", "Seu nome"), ["SAVE"] = ("GUARDAR", "SALVAR"),
            ["Shown on leaderboards. Letters, numbers and _ only (3–20)."] = ("Se muestra en la clasificación. Solo letras, números y _ (3–20).", "Aparece na classificação. Só letras, números e _ (3–20)."),
            ["Use 3–20 letters, numbers or _ (no spaces)."] = ("Usa 3–20 letras, números o _ (sin espacios).", "Use 3–20 letras, números ou _ (sem espaços)."),
            ["Saving…"] = ("Guardando…", "Salvando…"),
            ["You're offline. Your name will be saved locally."] = ("Sin conexión. Tu nombre se guardará en el teléfono.", "Você está offline. Seu nome será salvo no celular."),
            ["Couldn't save the name online. Try another name."] = ("No se pudo guardar el nombre. Prueba otro.", "Não foi possível salvar o nome. Tente outro."),
            ["COLLECTION"] = ("COLECCIÓN", "COLEÇÃO"), ["COLLECTION  {0}/{1}"] = ("COLECCIÓN  {0}/{1}", "COLEÇÃO  {0}/{1}"),
            ["Sunset"] = ("Atardecer", "Pôr do sol"), ["Rocket"] = ("Cohete", "Foguete"), ["Heart"] = ("Corazón", "Coração"),
            ["Beat levels to reveal each picture."] = ("Supera niveles para revelar cada imagen.", "Vença níveis para revelar cada imagem."),
            ["PICTURE PIECE!  {0} {1}/{2}"] = ("¡PIEZA DE IMAGEN!  {0} {1}/{2}", "PEÇA DA IMAGEM!  {0} {1}/{2}"),
            ["PICTURE COMPLETE!  +{0} ●"] = ("¡IMAGEN COMPLETA!  +{0} ●", "IMAGEM COMPLETA!  +{0} ●"),
            ["{0}  {1}/{2}"] = ("{0}  {1}/{2}", "{0}  {1}/{2}"),
            ["LANGUAGE"] = ("IDIOMA", "IDIOMA"),
            // missions, themes, trophies
            ["DAILY MISSIONS"] = ("MISIONES DIARIAS", "MISSÕES DIÁRIAS"), ["DONE"] = ("HECHO", "FEITO"), ["CLAIM"] = ("RECLAMAR", "RESGATAR"), ["GOOGLE CONNECTED ✓"] = ("GOOGLE CONECTADO ✓", "GOOGLE CONECTADO ✓"), ["CONNECTING…"] = ("CONECTANDO…", "CONECTANDO…"), ["CONTINUE WITH GOOGLE"] = ("CONTINUAR CON GOOGLE", "CONTINUAR COM O GOOGLE"), ["Connected! Your progress is saved to your Google account."] = ("¡Conectado! Tu progreso se guarda en tu cuenta de Google.", "Conectado! Seu progresso é salvo na sua conta do Google."), ["Play today's Daily challenge"] = ("Juega el reto diario de hoy", "Jogue o desafio diário de hoje"), ["Beat the AI once"] = ("Vence a la IA una vez", "Vença a IA uma vez"), ["Beat the AI {0} times"] = ("Vence a la IA {0} veces", "Vença a IA {0} vezes"), ["DAILY CHEST"] = ("COFRE DIARIO", "BAÚ DIÁRIO"), ["OPEN CHEST"] = ("ABRIR COFRE", "ABRIR BAÚ"), ["CHEST OPENED ✓"] = ("COFRE ABIERTO ✓", "BAÚ ABERTO ✓"), ["Finish all 3 missions for coins, a hammer and a shuffle"] = ("Completa las 3 misiones: monedas, un martillo y una mezcla", "Complete as 3 missões: moedas, um martelo e uma troca"), ["READY TO CLAIM!"] = ("¡LISTO PARA RECLAMAR!", "PRONTO PARA RESGATAR!"), ["DONE ✓"] = ("HECHO ✓", "FEITO ✓"),
            ["New missions every day. Coins unlock themes; they never buy an advantage."] = ("Nuevas misiones cada día. Las monedas desbloquean temas, nunca compran ventajas.", "Novas missões todo dia. Moedas liberam temas, nunca compram vantagens."),
            ["IN USE"] = ("EN USO", "EM USO"), ["USE"] = ("USAR", "USAR"),
            ["First Clear"] = ("Primera línea", "Primeira linha"), ["Clear your first line"] = ("Borra tu primera línea", "Limpe sua primeira linha"),
            ["Line Cleaner"] = ("Limpiador", "Limpador"), ["Clear 100 lines"] = ("Borra 100 líneas", "Limpe 100 linhas"),
            ["Line Legend"] = ("Leyenda de líneas", "Lenda das linhas"), ["Clear 1,000 lines"] = ("Borra 1.000 líneas", "Limpe 1.000 linhas"),
            ["Combo Starter"] = ("Primer combo", "Primeiro combo"), ["Reach a x3 combo"] = ("Logra un combo x3", "Faça um combo x3"),
            ["Combo King"] = ("Rey del combo", "Rei do combo"), ["Reach a x6 combo"] = ("Logra un combo x6", "Faça um combo x6"),
            ["Warming Up"] = ("Calentando", "Aquecendo"), ["Finish 10 games"] = ("Termina 10 partidas", "Termine 10 partidas"),
            ["Dedicated"] = ("Dedicado", "Dedicado"), ["Finish 100 games"] = ("Termina 100 partidas", "Termine 100 partidas"),
            ["High Scorer"] = ("Gran puntaje", "Grande pontuação"), ["Score 1,000 in one game"] = ("Haz 1.000 puntos en una partida", "Faça 1.000 pontos em uma partida"),
            ["Score Master"] = ("Maestro del puntaje", "Mestre da pontuação"), ["Score 5,000 in one game"] = ("Haz 5.000 puntos en una partida", "Faça 5.000 pontos em uma partida"),
            ["Star Collector"] = ("Coleccionista", "Colecionador"), ["Earn 30 level stars"] = ("Gana 30 estrellas de nivel", "Ganhe 30 estrelas de nível"),
            ["AI Slayer"] = ("Vence a la IA", "Derrota a IA"), ["Beat the Master AI"] = ("Vence a la IA maestra", "Vença a IA mestre"),
            ["Loyal Player"] = ("Jugador fiel", "Jogador fiel"), ["Reach a 7-day streak"] = ("Logra una racha de 7 días", "Faça uma sequência de 7 dias"),
            // templates (used with F)
            ["LEVEL {0}!   +{1} ●"] = ("¡NIVEL {0}!   +{1} ●", "NÍVEL {0}!   +{1} ●"), ["COMBO x{0}"] = ("COMBO x{0}", "COMBO x{0}"),
            ["AI x{0}!"] = ("¡IA x{0}!", "IA x{0}!"), ["STREAK x{0}"] = ("RACHA x{0}", "SEQUÊNCIA x{0}"),
            ["Score {0}. Second chance: 3 new pieces that fit!"] = ("Puntaje {0}. Segunda oportunidad: ¡3 piezas nuevas que caben!", "Pontuação {0}. Segunda chance: 3 peças novas que cabem!"),
            ["Score {0}"] = ("Puntaje {0}", "Pontuação {0}"), ["LV {0}   ·   STREAK {1}"] = ("NV {0}   ·   RACHA {1}", "NV {0}   ·   SEQUÊNCIA {1}"),
            ["LEVELS {0}★"] = ("NIVELES {0}★", "NÍVEIS {0}★"), ["PLAYER: {0}"] = ("JUGADOR: {0}", "JOGADOR: {0}"),
            ["BEST {0}   ·   {1}"] = ("RÉCORD {0}   ·   {1}", "RECORDE {0}   ·   {1}"), ["DAY {0} STREAK!"] = ("¡RACHA DE {0} DÍAS!", "SEQUÊNCIA DE {0} DIAS!"),
            ["Come back tomorrow for +{0}"] = ("Vuelve mañana por +{0}", "Volte amanhã para ganhar +{0}"),
            ["● {0}   ·   STREAK {1}   ·   BEST STREAK {2}"] = ("● {0}   ·   RACHA {1}   ·   MEJOR RACHA {2}", "● {0}   ·   SEQUÊNCIA {1}   ·   MELHOR {2}"),
            ["RANKS"] = ("RANGOS", "RANKS"),
            ["RANK UP: {0}!   +{1} ●"] = ("¡SUBES DE RANGO: {0}!   +{1} ●", "SUBIU DE RANK: {0}!   +{1} ●"),
            ["RANK UP!  +{0} ●{1}"] = ("¡RANGO NUEVO!  +{0} ●{1}", "NOVO RANK!  +{0} ●{1}"),
            ["  +1 HAMMER  +1 SHUFFLE"] = ("  +1 MARTILLO  +1 MEZCLA", "  +1 MARTELO  +1 EMBARALHAR"),
            ["Daily rank counts once a day. Come back tomorrow!"] = ("El rango diario cuenta una vez al día. ¡Vuelve mañana!", "O rank diário conta uma vez por dia. Volte amanhã!"),
            ["This AI is too easy for your rank. Try a harder one!"] = ("Esta IA es muy fácil para tu rango. ¡Prueba una más difícil!", "Esta IA é fácil demais para o seu rank. Tente uma mais difícil!"),
            ["{0} RP to {1}"] = ("{0} RP para {1}", "{0} RP para {1}"),
            ["You are a Legend!"] = ("¡Eres una Leyenda!", "Você é uma Lenda!"),
            ["{0}   ·   {1} RP to {2}"] = ("{0}   ·   {1} RP para {2}", "{0}   ·   {1} RP para {2}"),
            ["Play to climb. Every new rank pays coins, and every new tier a hammer and a shuffle."] = ("Juega para subir. Cada rango da monedas y cada nivel un martillo y una mezcla.", "Jogue para subir. Cada rank dá moedas e cada nível um martelo e um embaralhar."),
            ["Every game earns RP from your score."] = ("Cada partida da RP según tu puntuación.", "Cada partida dá RP pela sua pontuação."),
            ["Your first Daily game each day counts."] = ("Cuenta tu primera partida Diaria de cada día.", "Conta sua primeira partida Diária de cada dia."),
            ["Beat the AI to win RP. Losses cost a little."] = ("Vence a la IA para ganar RP. Perder cuesta un poco.", "Vença a IA para ganhar RP. Perder custa um pouco."),
            ["Top rank reached!"] = ("¡Rango máximo alcanzado!", "Rank máximo alcançado!"),
            ["Tap a mode to play it."] = ("Toca un modo para jugarlo.", "Toque num modo para jogá-lo."),
            ["BRONZE"] = ("BRONCE", "BRONZE"),
            ["SILVER"] = ("PLATA", "PRATA"),
            ["GOLD"] = ("ORO", "OURO"),
            ["DIAMOND"] = ("DIAMANTE", "DIAMANTE"),
            ["CROWN"] = ("CORONA", "COROA"),
            ["MASTER"] = ("MAESTRO", "MESTRE"),
            ["CHAMPION"] = ("CAMPEÓN", "CAMPEÃO"),
            ["LEGEND"] = ("LEYENDA", "LENDA"),
            ["KING  #{0}"] = ("REY  #{0}", "REI  #{0}"),
            ["Link your Google account to chase the KING title (top 100)."] = ("Vincula tu cuenta de Google para optar al título de REY (top 100).", "Vincule sua conta Google para disputar o título de REI (top 100)."),
            ["King"] = ("Rey", "Rei"),
            ["Earn more stars on a level to gain RP."] = ("Gana más estrellas en un nivel para sumar RP.", "Ganhe mais estrelas em um nível para somar RP."),
            ["TROPHIES  {0}/{1}"] = ("TROFEOS  {0}/{1}", "TROFÉUS  {0}/{1}"), ["LEVEL {0}   ·   {1}/{2} XP"] = ("NIVEL {0}   ·   {1}/{2} XP", "NÍVEL {0}   ·   {1}/{2} XP"),
            ["HINT {0}"] = ("PISTA {0}", "DICA {0}"), ["LEVEL {0}"] = ("NIVEL {0}", "NÍVEL {0}"), ["MOVES LEFT  {0}"] = ("MOVIMIENTOS  {0}", "JOGADAS  {0}"),
            ["PLAYER {0}"] = ("JUGADOR {0}", "JOGADOR {0}"), ["AI ({0})"] = ("IA ({0})", "IA ({0})"),
            ["PLAYER {0}\nYOUR TURN"] = ("JUGADOR {0}\nTU TURNO", "JOGADOR {0}\nSUA VEZ"), ["BEST {0}"] = ("RÉCORD {0}", "RECORDE {0}"),
            ["SCORE {0}"] = ("PUNTAJE {0}", "PONTUAÇÃO {0}"), ["SCORE {0} / {1}"] = ("PUNTAJE {0} / {1}", "PONTUAÇÃO {0} / {1}"),
            ["PLAYER {0} WINS!"] = ("¡GANA EL JUGADOR {0}!", "JOGADOR {0} VENCEU!"),
            ["Beat my {0} in Block Drop! Code: {1}"] = ("¡Supera mis {0} puntos en Block Drop! Código: {1}", "Supere meus {0} pontos no Block Drop! Código: {1}"),
            ["So close! Only {0} points from your best."] = ("¡Casi! Solo {0} puntos para tu récord.", "Quase! Só {0} pontos do seu recorde."),
            ["Codes have {0} letters/digits, e.g. K7Q2MX"] = ("Los códigos tienen {0} letras/números, p. ej. K7Q2MX", "Os códigos têm {0} letras/números, ex. K7Q2MX"),
            ["Saved! You are {0}"] = ("¡Guardado! Eres {0}", "Salvo! Você é {0}"), ["Please choose a different name."] = ("Elige otro nombre, por favor.", "Escolha outro nome, por favor."), ["LANGUAGE: {0}"] = ("IDIOMA: {0}", "IDIOMA: {0}"),
            ["Clear {0} lines"] = ("Borra {0} líneas", "Limpe {0} linhas"), ["Score {0} points in total"] = ("Haz {0} puntos en total", "Faça {0} pontos no total"),
            ["Place {0} pieces"] = ("Coloca {0} piezas", "Coloque {0} peças"), ["Reach a x{0} combo"] = ("Logra un combo x{0}", "Faça um combo x{0}"),
            ["Finish {0} games"] = ("Termina {0} partidas", "Termine {0} partidas"), ["Earn {0} level stars"] = ("Gana {0} estrellas de nivel", "Ganhe {0} estrelas de nível"),
            // new look
            ["Fair hands. Every game."] = ("Manos justas. Cada partida.", "Mãos justas. Toda partida."),
            ["One cell away from a {0}-line clear!"] = ("¡A una casilla de borrar {0} líneas!", "A uma casa de limpar {0} linhas!"),
            ["One cell away from a clear!"] = ("¡A una casilla de borrar una línea!", "A uma casa de limpar uma linha!"),
            ["MOTION: FULL"] = ("MOVIMIENTO: COMPLETO", "MOVIMENTO: COMPLETO"), ["MOTION: REDUCED"] = ("MOVIMIENTO: REDUCIDO", "MOVIMENTO: REDUZIDO"),
            ["PATTERNS: ON"] = ("PATRONES: SÍ", "PADRÕES: SIM"), ["PATTERNS: OFF"] = ("PATRONES: NO", "PADRÕES: NÃO"),
            ["HARD LEVEL"] = ("NIVEL DIFÍCIL", "NÍVEL DIFÍCIL"), ["SUPER HARD LEVEL"] = ("NIVEL SÚPER DIFÍCIL", "NÍVEL SUPERDIFÍCIL"),
            ["CHAPTER {0}"] = ("CAPÍTULO {0}", "CAPÍTULO {0}"), ["GO TO {0}"] = ("IR A {0}", "IR PARA {0}"),
            ["Sunny Meadow"] = ("Prado Soleado", "Prado Ensolarado"), ["Pink Dusk"] = ("Atardecer Rosa", "Crepúsculo Rosa"), ["Blue Lagoon"] = ("Laguna Azul", "Lagoa Azul"),
            ["Violet Night"] = ("Noche Violeta", "Noite Violeta"), ["Golden Dunes"] = ("Dunas Doradas", "Dunas Douradas"), ["Mint Frost"] = ("Escarcha de Menta", "Gelo de Menta"),
            ["A little help: the target is lower"] = ("Un poco de ayuda: la meta es menor", "Uma ajudinha: a meta está menor"), ["+{0} ●"] = ("+{0} ●", "+{0} ●"),
            ["Reach {0} points"] = ("Llega a {0} puntos", "Chegue a {0} pontos"), ["{0} moves"] = ("{0} movimientos", "{0} jogadas"),
            ["NEW LOOK ON"] = ("NUEVO ESTILO ACTIVADO", "NOVO VISUAL LIGADO"), ["NEW LOOK OFF"] = ("NUEVO ESTILO DESACTIVADO", "NOVO VISUAL DESLIGADO"),
            // levels 2.0: chapters, goals, blockers
            ["Gem"] = ("Gema", "Gema"), ["Peaks"] = ("Cumbres", "Picos"), ["Moon"] = ("Luna", "Lua"), ["Reef"] = ("Arrecife", "Recife"),
            ["Blossom"] = ("Flor", "Flor"), ["Castle"] = ("Castillo", "Castelo"), ["Crown"] = ("Corona", "Coroa"),
            ["Fill a row or column to clear it. Reach the target score!"] = ("Llena una fila o columna para borrarla. ¡Alcanza el puntaje!", "Complete uma linha ou coluna para limpá-la. Alcance a pontuação!"),
            ["New goal: clear lines! Every row or column counts."] = ("Nuevo objetivo: ¡borra líneas! Cuenta cada fila o columna.", "Novo objetivo: limpe linhas! Cada linha ou coluna conta."),
            ["New goal: clear every stone. Stones vanish with their line."] = ("Nuevo objetivo: borra todas las piedras. Desaparecen con su línea.", "Novo objetivo: limpe todas as pedras. Elas somem com a linha."),
            ["New! Gems. Clear a gem's row or column to collect it."] = ("¡Nuevo! Gemas. Borra su fila o columna para recogerla.", "Novo! Gemas. Limpe a linha ou coluna da gema para coletá-la."),
            ["New! Ice. It takes two clears: the first cracks it to stone."] = ("¡Nuevo! Hielo. Necesita dos borrados: el primero lo vuelve piedra.", "Novo! Gelo. Precisa de duas limpezas: a primeira vira pedra."),
            ["Double goals! Complete both to win."] = ("¡Doble objetivo! Cumple los dos para ganar.", "Objetivo duplo! Complete os dois para vencer."),
            ["Ice walls! Clear lines and break every stone."] = ("¡Muros de hielo! Borra líneas y rompe todas las piedras.", "Muros de gelo! Limpe linhas e quebre todas as pedras."),
            ["Frozen garden: gems hide among ice. Clear them all."] = ("Jardín helado: las gemas se esconden entre el hielo. Bórralas todas.", "Jardim congelado: gemas entre o gelo. Limpe todas."),
            ["Storm the castle: big scores and icy walls."] = ("Asalta el castillo: grandes puntajes y muros de hielo.", "Invada o castelo: pontuações altas e muros de gelo."),
            ["The final chapter. Collect the crown jewels!"] = ("El capítulo final. ¡Reúne las joyas de la corona!", "O capítulo final. Colete as joias da coroa!"),
            ["So close! Only {0} points to go."] = ("¡Casi! Solo faltan {0} puntos.", "Quase! Faltam só {0} pontos."),
            ["So close! Just 1 more line."] = ("¡Casi! Solo 1 línea más.", "Quase! Só mais 1 linha."), ["So close! Just {0} more lines."] = ("¡Casi! Solo {0} líneas más.", "Quase! Só mais {0} linhas."),
            ["So close! Only 1 stone left."] = ("¡Casi! Solo queda 1 piedra.", "Quase! Só falta 1 pedra."), ["So close! Only {0} stones left."] = ("¡Casi! Solo quedan {0} piedras.", "Quase! Só faltam {0} pedras."),
            ["So close! Only 1 gem left."] = ("¡Casi! Solo queda 1 gema.", "Quase! Só falta 1 gema."), ["So close! Only {0} gems left."] = ("¡Casi! Solo quedan {0} gemas.", "Quase! Só faltam {0} gemas."),
            ["GEM!"] = ("¡GEMA!", "GEMA!"), ["{0} GEMS!"] = ("¡{0} GEMAS!", "{0} GEMAS!"), ["GOAL COMPLETE!"] = ("¡OBJETIVO CUMPLIDO!", "OBJETIVO CONCLUÍDO!"),
            ["NO ROOM! FRESH PIECES"] = ("¡SIN ESPACIO! PIEZAS NUEVAS", "SEM ESPAÇO! PEÇAS NOVAS"), ["BONUS BLAST! {0} MOVES"] = ("¡EXPLOSIÓN BONUS! {0} MOVIMIENTOS", "EXPLOSÃO BÔNUS! {0} JOGADAS"),
            ["+{0} MOVES!"] = ("¡+{0} MOVIMIENTOS!", "+{0} JOGADAS!"), ["+{0} MOVES  {1} ●"] = ("+{0} MOVIMIENTOS  {1} ●", "+{0} JOGADAS  {1} ●"),
            ["Keep going: 5 more moves could do it."] = ("Sigue: 5 movimientos más podrían bastar.", "Continue: mais 5 jogadas podem bastar."),
            ["Day 7 prize: a free hammer!"] = ("Premio del día 7: ¡un martillo gratis!", "Prêmio do dia 7: um martelo grátis!"),
            ["BOSS DOWN!  +{0} ●  +1 HAMMER"] = ("¡JEFE VENCIDO!  +{0} ●  +1 MARTILLO", "CHEFE DERROTADO!  +{0} ●  +1 MARTELO"),
            ["PREVIEW ON: ALL LEVELS OPEN"] = ("VISTA PREVIA: TODOS LOS NIVELES ABIERTOS", "PRÉVIA: TODOS OS NÍVEIS ABERTOS"), ["PREVIEW OFF"] = ("VISTA PREVIA DESACTIVADA", "PRÉVIA DESLIGADA"),
            ["SUPER HARD"] = ("SÚPER DIFÍCIL", "SUPER DIFÍCIL"), ["HARD LEVEL"] = ("NIVEL DIFÍCIL", "NÍVEL DIFÍCIL"), ["HARD"] = ("DIFÍCIL", "DIFÍCIL"),
            ["NEW!"] = ("¡NUEVO!", "NOVO!"), ["in {0} moves"] = ("en {0} movimientos", "em {0} jogadas"), ["(+{0} helping hand)"] = ("(+{0} de ayuda)", "(+{0} de ajuda)"),
            ["Score {0} points"] = ("Haz {0} puntos", "Faça {0} pontos"), ["Clear the stone"] = ("Borra la piedra", "Limpe a pedra"), ["Clear all {0} stones"] = ("Borra las {0} piedras", "Limpe as {0} pedras"),
            ["Collect the gem"] = ("Recoge la gema", "Colete a gema"), ["Collect all {0} gems"] = ("Recoge las {0} gemas", "Colete as {0} gemas"),
            ["SCORE {0}/{1}"] = ("PUNTOS {0}/{1}", "PONTOS {0}/{1}"), ["LINES {0}/{1}"] = ("LÍNEAS {0}/{1}", "LINHAS {0}/{1}"),
            ["STONES {0}/{1}"] = ("PIEDRAS {0}/{1}", "PEDRAS {0}/{1}"), ["GEMS {0}/{1}"] = ("GEMAS {0}/{1}", "GEMAS {0}/{1}"),
            ["CHAPTER {0}"] = ("CAPÍTULO {0}", "CAPÍTULO {0}"), ["PICTURES {0}/{1}"] = ("IMÁGENES {0}/{1}", "IMAGENS {0}/{1}"), ["MOVES"] = ("MOVIMIENTOS", "JOGADAS"),
            ["Helping hand: +{0} moves on your next try!"] = ("Una ayuda: ¡+{0} movimientos en tu próximo intento!", "Uma ajuda: +{0} jogadas na próxima tentativa!"),
            ["SCORE {0}  (BONUS +{1})"] = ("PUNTAJE {0}  (BONUS +{1})", "PONTUAÇÃO {0}  (BÔNUS +{1})"),
            // themes and shop
            ["Jewel"] = ("Joya", "Joia"), ["Neon"] = ("Neón", "Neon"), ["Candy"] = ("Dulce", "Doce"), ["Ocean"] = ("Océano", "Oceano"),
            ["Forest"] = ("Bosque", "Floresta"), ["Arcade"] = ("Arcade", "Fliperama"), ["Sakura"] = ("Sakura", "Sakura"), ["Midnight"] = ("Medianoche", "Meia-noite"),
            ["Desert"] = ("Desierto", "Deserto"), ["Aurora"] = ("Aurora", "Aurora"), ["Galaxy"] = ("Galaxia", "Galáxia"), ["Diamond"] = ("Diamante", "Diamante"),
            ["Royal Gold"] = ("Oro real", "Ouro real"), ["Glacier"] = ("Glaciar", "Geleira"), ["Crown Jewels"] = ("Joyas de la corona", "Joias da coroa"), ["Starlight"] = ("Luz de estrellas", "Luz das estrelas"),
            ["Calm greens and warm wood"] = ("Verdes tranquilos y madera cálida", "Verdes calmos e madeira quente"),
            ["Neon on midnight black"] = ("Neón sobre negro medianoche", "Neon no preto da meia-noite"),
            ["Spring blossom pinks"] = ("Rosas de flor de primavera", "Rosas de flor de primavera"),
            ["City lights after dark"] = ("Luces de ciudad de noche", "Luzes da cidade à noite"),
            ["Terracotta and gold sand"] = ("Terracota y arena dorada", "Terracota e areia dourada"),
            ["Northern lights on every block"] = ("Auroras boreales en cada bloque", "Aurora boreal em cada bloco"),
            ["Deep space violet"] = ("Violeta del espacio profundo", "Violeta do espaço profundo"),
            ["Ice-clear luxury"] = ("Lujo transparente como el hielo", "Luxo claro como gelo"),
            ["Solid gold on royal purple"] = ("Oro macizo sobre púrpura real", "Ouro maciço no roxo real"),
            ["Finish chapter 5"] = ("Termina el capítulo 5", "Termine o capítulo 5"), ["Finish all 100 levels"] = ("Termina los 100 niveles", "Termine os 100 níveis"),
            ["Earn 250 level stars"] = ("Gana 250 estrellas de nivel", "Ganhe 250 estrelas de nível"),
            ["STARTER"] = ("INICIALES", "INICIAIS"), ["PREMIUM"] = ("PREMIUM", "PREMIUM"), ["LEGENDARY"] = ("LEGENDARIOS", "LENDÁRIOS"), ["EXCLUSIVE"] = ("EXCLUSIVOS", "EXCLUSIVOS"),
            ["SHOP"] = ("TIENDA", "LOJA"), ["BUNDLES"] = ("PAQUETES", "PACOTES"), ["+{0}% MORE"] = ("+{0}% MÁS", "+{0}% MAIS"), ["OWNED"] = ("TUYO", "SEU"), ["RESTORE"] = ("RESTAURAR", "RESTAURAR"), ["{0}/{1} CHAPTERS"] = ("{0}/{1} CAPÍTULOS", "{0}/{1} CAPÍTULOS"),
            ["EXCLUSIVE THEME UNLOCKED: {0}!"] = ("¡TEMA EXCLUSIVO DESBLOQUEADO: {0}!", "TEMA EXCLUSIVO DESBLOQUEADO: {0}!"),
            ["THANK YOU! {0}"] = ("¡GRACIAS! {0}", "OBRIGADO! {0}"),
            ["Purchases open when Block Drop launches on Google Play."] = ("Las compras se abren cuando Block Drop llegue a Google Play.", "As compras abrem quando o Block Drop chegar ao Google Play."),
            ["Pouch of coins"] = ("Bolsa de monedas", "Bolsa de moedas"), ["Chest of coins"] = ("Cofre de monedas", "Baú de moedas"), ["Vault of coins"] = ("Bóveda de monedas", "Cofre de moedas"),
            ["Treasury of coins"] = ("Tesoro de monedas", "Tesouro de moedas"), ["Remove ads"] = ("Quitar anuncios", "Remover anúncios"), ["Royal bundle"] = ("Paquete real", "Pacote real"),
            ["Legends collection"] = ("Colección de leyendas", "Coleção de lendas"), ["{0} theme"] = ("tema {0}", "tema {0}"), ["no ads"] = ("sin anuncios", "sem anúncios"),
            ["CHAPTER GIFT: +{0} HAMMERS, +{0} SHUFFLES"] = ("REGALO DE CAPÍTULO: +{0} MARTILLOS, +{0} MEZCLAS", "PRESENTE DO CAPÍTULO: +{0} MARTELOS, +{0} EMBARALHADAS"),
            ["Tap a block to smash it"] = ("Toca un bloque para romperlo", "Toque um bloco para quebrá-lo"),
            ["Ice cracks to stone. Smashed gems count."] = ("El hielo se vuelve piedra. Las gemas rotas cuentan.", "O gelo vira pedra. Gemas quebradas contam."),
            ["CANCEL"] = ("CANCELAR", "CANCELAR"), ["HAMMER"] = ("MARTILLO", "MARTELO"), ["SHUFFLE"] = ("MEZCLAR", "EMBARALHAR"),
            ["HINT\n×{0}"] = ("PISTA\n×{0}", "DICA\n×{0}"), ["SHUFFLE!"] = ("¡MEZCLA!", "EMBARALHADO!"), ["SMASH!"] = ("¡ZAS!", "PÁ!"),
            ["CLOSE"] = ("CERRAR", "FECHAR"), ["BOOST"] = ("AYUDAS", "AJUDAS"), ["Booster box"] = ("Caja de ayudas", "Caixa de ajudas"),
            ["{0} hammers"] = ("{0} martillos", "{0} martelos"), ["{0} shuffles"] = ("{0} mezclas", "{0} embaralhadas"),
            ["POPULAR"] = ("POPULAR", "POPULAR"), ["BEST VALUE"] = ("MEJOR VALOR", "MELHOR VALOR"), ["ALL LEGENDARY"] = ("TODOS LOS LEGENDARIOS", "TODOS OS LENDÁRIOS"),
        };
    }
}
