# Utilizarea AI

Am folosit AI doar ca asistent de programare, nu pentru a genera jocul de la zero.

## Instrumente folosite

- GitHub Copilot in VS Code (model: GPT-5.4)

## Cum am folosit fiecare instrument

- Brainstorming de idei: AI m-a ajutat cu idei de gameplay, structura de roguelite si clarificarea fluxului camp -> run -> progres permanent.
- Optimizare si imbunatatire: AI a oferit sugestii pentru organizarea codului si mici ajustari de implementare.
- Asistenta de refactorizare (stil agentic/chat): am impartit codul din `GameLogic.cs` in `Entities.cs`, `SaveData.cs` si partea ramasa in `GameLogic.cs`.
- Curatare punctuala de cod: am redenumit `PersistHighScoreAsync` in `PersistHighScore`, deoarece metoda era sincronă.
- Fix tehnic punctual: am modificat `SdlContext.TryGetProcAddress` sa foloseasca `NativeLibrary.TryGetExport`.
- Fix de build: am inlocuit `Rect` cu `Rectangle<int>` din `Silk.NET.Maths`, pentru compatibilitate cu Silk.NET 2.23.0.

Toata logica de gameplay, regulile jocului, liniile de cod si implementarea finala imi apartin. AI a fost folosit ca suport pentru idei, optimizare si imbunatatiri, iar contributia AI la cod a fost limitata la asistenta explicit mentionata mai jos.

## Fisiere sau regiuni complet generate de AI

- `Effects.cs`: complet generat de AI. Sistem de particule, screen shake, screen flash si utilitare de culoare (HSV->RGB). Intreg fisierul este incadrat cu `// AI-generated` si `// end AI-generated`.
- `Entities.cs`: blocurile AI sunt doar `HashSet<GridPosition> _occupied` si metoda `WouldHitSelf`. Fiecare bloc este marcat inline cu `// AI-generated` si `// end AI-generated`.

## Fisiere la care AI a oferit asistenta

- `Entities.cs`: creat prin mutarea tipurilor existente (scrise de mine) din `GameLogic.cs`.
- `SaveData.cs`: creat prin mutarea codului existent de salvare/persistenta (scris de mine) din `GameLogic.cs`.
- `GameLogic.cs`: curatat dupa extractie; o redenumire de metoda.
- `SdlContext.cs`: o metoda (`TryGetProcAddress`) rescrisa intr-o forma echivalenta, mai curata.
