# Snake

Snake este un joc clasic construit peste starter-ul SDL ProjectSkeleton. Controlezi un șarpe care se deplasează continuu pe o grilă de 20x20 de celule. Scopul este să mănânci mâncarea roșie pentru a crește în lungime și a aduna scor. Șarpele accelerează ușor pe măsură ce scorul crește. Pierzi dacă te lovești de un perete sau de propriul corp. Câștigi dacă umpli toată tabla. Cel mai bun scor se păstrează pe disc între rulări.

## Acoperire Cerinte
- Bucla de joc: input -> update -> render implementata in bucla principala SDL din `Program.cs`.
- Input utilizator: directie cu sageti sau WASD, start/restart cu Enter/Space, restart cu R, iesire cu Escape.
- Conditie de lose: pierzi cand te lovesti de un perete sau de propriul corp.
- Conditie de win: castigi daca sarpele umple toata tabla (20x20 celule).
- Stare non-triviala: corpul sarpelui (lista de celule), pozitia mancarii, bonusul temporizat, scorul curent, high score.
- Persistenta: high score-ul se salveaza pe disc si se incarca la pornire.

## Mecanici de Joc
- Mancare normala: creste sarpele cu o celula si aduna puncte (inmultite cu multiplicatorul de combo).
- Bonus auriu (temporizat): apare ocazional dupa ce mananci, are un cronometru si valoreaza intre 2 si 8 puncte (si el inmultit cu combo). Cu cat il prinzi mai repede, cu atat valoreaza mai mult. Nu creste sarpele. Clipeste cand e gata sa expire.
- Obstacole (pereti interiori): segmente gri scatterate pe tabla. Daca te lovesti de ele pierzi. Exista mereu un coridor liber pe randul de start.
- Combo: daca mananci din nou inainte sa expire fereastra de combo, multiplicatorul creste (pana la x5). Lasi sa treaca timpul si se reseteaza.
- Mod wrap: este mereu activ; cand iesi pe o margine, apari pe marginea opusa. Obstacolele raman mortale.
- Viteza creste usor pe masura ce aduni scor.

## Efecte Vizuale
- Mancarea pulseaza in dimensiune si luminozitate.
- Corpul sarpelui are un shimmer animat de culoare care curge de la cap spre coada.
- Cand mananci: explozie de particule si un mic screen shake.
- La game over: sarpele explodeaza in particule, screen shake puternic si flash rosu.
- La victorie: flash verde si confetti care cad pe ecran.

## Build si Rulare
1. Instaleaza .NET 10 SDK.
2. Din radacina repository-ului ruleaza:

```powershell
dotnet run
```

## Controale
- Sageti sau WASD: schimba directia sarpelui
- Enter sau Space: porneste / reincepe jocul
- Wrap-ul este mereu activ (nu are toggle)
- R: reincepe dupa game over
- Escape: iesire din joc

## Locatie Fisier Save (Windows)
- `%LOCALAPPDATA%/TheAdventure/save.json`
