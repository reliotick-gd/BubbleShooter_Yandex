using UnityEditor;
using UnityEngine;

namespace CozyAnimalTown.EditorTools
{
    /// <summary>
    /// Заставляет любой звук из Assets/Resources импортироваться как Decompress On Load.
    ///
    /// ЗАЧЕМ: Unity WebGL (Audio.js, JS_Sound_Load) для клипов Compressed In Memory тяжелее
    /// 128 КБ создаёт настоящий HTML-элемент `new Audio()` и вешает его через
    /// createMediaElementSource — но только в НЕ-Chromium браузерах (проверка `window.chrome`),
    /// то есть во всех браузерах iOS. WebKit поднимает такому элементу системный плеер в
    /// «Пункте управления», а это нарушение п.1.6.1.6 требований Яндекса: «в любых браузерах
    /// не отображается системный плеер, вызываемый игрой». Черновик уже отклоняли по нему.
    ///
    /// Decompress On Load уводит клип на decodeAudioData → AudioBufferSourceNode, медиа-элемент
    /// не создаётся вообще. Настройка живёт в .meta и легко теряется при переимпорте или при
    /// добавлении нового файла — поэтому она форсится здесь, а не только руками в инспекторе.
    /// AudioService уже пробует грузить Resources/Audio/* — дыра открыта, закрываем заранее.
    /// </summary>
    public class ForceWebAudioImportSettings : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (assetPath.Replace('\\', '/').IndexOf("/Resources/", System.StringComparison.Ordinal) < 0) return;

            var importer = (AudioImporter)assetImporter;
            var s = importer.defaultSampleSettings;
            if (s.loadType == AudioClipLoadType.DecompressOnLoad) return;

            s.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = s;
            Debug.Log($"[Audio] {assetPath}: Load Type → Decompress On Load (требование Яндекса п.1.6.1.6)");
        }
    }
}
