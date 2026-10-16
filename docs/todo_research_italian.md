1. rimuovere console modding, levo una dipendenza da una dll che non serve
2. portare a net framework recente
3. vedere di capire come cambiare il renderer sogno
4. migliorare carbin viewer, magari cercando di capire quali sono veramente i materiali da usare.
4.a studiare i materiali del gioco. Capire se sono hardcoded o se sono file .shader da qualche parte
4.a.1 capire se possibile usare shader fm in viewer per sntrprima real time adpetto.
4.b capire shader con morph per ruote, indagare sul morph in generale @Jew dice
it's dependant on the wheel having a morph target in its package. 
sembra un flag che viaggia insieme a shader e flag is stock.parla di un certo tex, larger tex = resizable

4.b.1controllare morph target cerchio bmw e30 non scala mentre s60 scala.sempre @Jew 
You need morph targets in the wheel model for Forza to scale them to size

Morph_LODO

5. editor camera su base fov del gioco e viewport
6. andare a creare un tab/pulsante delle impostazioni
7. tool di conversione farlo a mo di MDI
8a. Prendere in mano modello fh1/2 traffico e vedere
8b. Prendere in mano modello FM2
8c. fix creazione ruote manuali @delete.diskord
  8c - i used forza studio to export an fm3 wheel to obj, then opened an fm4 wheel zip in forzaeditor and attempted to replace 
       the wheel with the obj i exported, resulting in the "LOD 0 and LODs 1+ live in different files" error. however i did 
       try the fm3 conversion (port car from another forza) in the new commit a little while ago and the wheel zip it output 
       seems to work perfectly in fm4.
9. documentazione seria delle scoperte dei file
10. vedere feedback discord. Ancora pochi utilizzatori.
