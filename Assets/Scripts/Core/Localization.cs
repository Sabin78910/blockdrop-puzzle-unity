using System;
using System.Collections.Generic;
using System.Globalization;

namespace BlockDrop.Core
{
    public enum Lang { En, Es, Pt }

    /// <summary>English text is the key. Spanish (Latin America) and Portuguese (Brazil) first: Brazil and Mexico
    /// are the top growth markets for block puzzles. Missing keys fall back to English.</summary>
    public static class Loc
    {
        public static Lang Current = Lang.En;

        public static readonly string[] Names = { "ENGLISH", "ESPAÑOL", "PORTUGUÊS" };

        public static Lang FromCode(string code)
        {
            code = (code ?? "").ToLowerInvariant();
            return code.StartsWith("es") ? Lang.Es : code.StartsWith("pt") ? Lang.Pt : Lang.En;
        }

        public static string Code(Lang l) => l == Lang.Es ? "es" : l == Lang.Pt ? "pt" : "en";

        public static string L(string en)
        {
            if (Current == Lang.En || string.IsNullOrEmpty(en) || !Table.TryGetValue(en, out var t)) return en;
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
            ["DAILY MISSIONS"] = ("MISIONES DIARIAS", "MISSÕES DIÁRIAS"), ["DONE"] = ("HECHO", "FEITO"), ["DONE ✓"] = ("HECHO ✓", "FEITO ✓"),
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
            ["TROPHIES  {0}/{1}"] = ("TROFEOS  {0}/{1}", "TROFÉUS  {0}/{1}"), ["LEVEL {0}   ·   {1}/{2} XP"] = ("NIVEL {0}   ·   {1}/{2} XP", "NÍVEL {0}   ·   {1}/{2} XP"),
            ["HINT {0}"] = ("PISTA {0}", "DICA {0}"), ["LEVEL {0}"] = ("NIVEL {0}", "NÍVEL {0}"), ["MOVES LEFT  {0}"] = ("MOVIMIENTOS  {0}", "JOGADAS  {0}"),
            ["PLAYER {0}"] = ("JUGADOR {0}", "JOGADOR {0}"), ["AI ({0})"] = ("IA ({0})", "IA ({0})"),
            ["PLAYER {0}\nYOUR TURN"] = ("JUGADOR {0}\nTU TURNO", "JOGADOR {0}\nSUA VEZ"), ["BEST {0}"] = ("RÉCORD {0}", "RECORDE {0}"),
            ["SCORE {0}"] = ("PUNTAJE {0}", "PONTUAÇÃO {0}"), ["SCORE {0} / {1}"] = ("PUNTAJE {0} / {1}", "PONTUAÇÃO {0} / {1}"),
            ["PLAYER {0} WINS!"] = ("¡GANA EL JUGADOR {0}!", "JOGADOR {0} VENCEU!"),
            ["Beat my {0} in Block Drop! Code: {1}"] = ("¡Supera mis {0} puntos en Block Drop! Código: {1}", "Supere meus {0} pontos no Block Drop! Código: {1}"),
            ["So close! Only {0} points from your best."] = ("¡Casi! Solo {0} puntos para tu récord.", "Quase! Só {0} pontos do seu recorde."),
            ["Codes have {0} letters/digits, e.g. K7Q2MX"] = ("Los códigos tienen {0} letras/números, p. ej. K7Q2MX", "Os códigos têm {0} letras/números, ex. K7Q2MX"),
            ["Saved! You are {0}"] = ("¡Guardado! Eres {0}", "Salvo! Você é {0}"), ["LANGUAGE: {0}"] = ("IDIOMA: {0}", "IDIOMA: {0}"),
            ["Clear {0} lines"] = ("Borra {0} líneas", "Limpe {0} linhas"), ["Score {0} points in total"] = ("Haz {0} puntos en total", "Faça {0} pontos no total"),
            ["Place {0} pieces"] = ("Coloca {0} piezas", "Coloque {0} peças"), ["Reach a x{0} combo"] = ("Logra un combo x{0}", "Faça um combo x{0}"),
            ["Finish {0} games"] = ("Termina {0} partidas", "Termine {0} partidas"), ["Earn {0} level stars"] = ("Gana {0} estrellas de nivel", "Ganhe {0} estrelas de nível"),
        };
    }
}
