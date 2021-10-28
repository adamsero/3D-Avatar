using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine.UI;

public class AnimationManager : MonoBehaviour {
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private Dictionary<string, int> blendNamesMap = new Dictionary<string, int>();
    private BlendShapeInterpolator blendShapeInterpolator;

    void Awake() {
        DataManager.loadBlendShapeWeights();
    }

    void Start() {
        Application.targetFrameRate = 144;
        blendShapeInterpolator = new BlendShapeInterpolator(this);
        GameObject head = GameObject.Find("Head");
        skinnedMeshRenderer = head.GetComponent<SkinnedMeshRenderer>();
        setupBlendShapesInfo();
    }

    public void speak(string data) {
        string[] dataSplit = data.Split(new string[] { "###" }, StringSplitOptions.None);
        animateFaceForSentence(dataSplit[0], dataSplit[1]);
    }

    private void setupBlendShapesInfo() {
        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        for(int i = 1; i < mesh.blendShapeCount; i++) {
            blendNamesMap.Add(mesh.GetBlendShapeName(i), i);
        }
    }

    private void setBlendShapeWeight(string name, float value) {
        int blendShapeIndex = getBlendShapeIndex(name);
        skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, value);
    }

    private int getBlendShapeIndex(string name) {
        int index;
        bool foundIndex = blendNamesMap.TryGetValue(name, out index);
        if(!foundIndex) {
            Debug.Log("Could not find blend shape with name " + name);
        }
        return index;
    }
    
    
    void Update() {
        countFrames();
        blendShapeInterpolator.Update();
    }

    //###TEMPORARY###
    int frames = 0;
    long ts = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
    private void countFrames() {
        frames++;
        if(frames == 100) {
            long tsNow = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            int fps = (int)(1e5 / (tsNow - ts));
            frames = 0;
            ts = tsNow;
            GameObject.Find("DebugLog").GetComponent<Text>().text = "FPS: " + fps;
        }
    }
    //###TEMPORARY###

    public void animateFaceForSentence(string sentence, string timestampsJoined) {
        string uppercase = sentence.ToUpper();
		StringBuilder sb = new StringBuilder();
        foreach(char ch in uppercase.ToCharArray()) {
			int ascii = (int)ch;
			if(ascii == 32 || (ascii > 64 && ascii < 91)) {
				sb.Append(ch);
			}
		}
		string cleanSentence = sb.ToString();

        string[] words = cleanSentence.Split(' ');
        string[] timestamps = timestampsJoined.Split(',');
        Dictionary<string, float> timestampsForWords = new Dictionary<string, float>();
        for(int i = 0; i < words.Length; i++) {
            timestampsForWords.Add(words[i], float.Parse(timestamps[i], System.Globalization.CultureInfo.InvariantCulture));
        }
        blendShapeInterpolator.interpolateForSentence(words, timestampsForWords);
    }

    private class BlendShapeInterpolator {
        private AnimationManager animationManager;
        Dictionary<string, float> timestampsForWords = null;
        private List<Tuple<string, float>> characterLengths = new List<Tuple<string, float>>();
        private bool speak = false;
        private int currentCharIndex = -1;
        private long currentReferenceTS = 0;
        private long startSentenceTS;
        private string prevChar = null;
        private string currChar = null;

        public BlendShapeInterpolator(AnimationManager animationManager) {
            this.animationManager = animationManager;
        }

        public void interpolateForSentence(string[] words, Dictionary<string, float> timestampsForWords) {
            this.timestampsForWords = timestampsForWords;
            speak = true;
            currentCharIndex = 0;
            currentReferenceTS = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            startSentenceTS = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            characterLengths.Clear();
            for(int i = 0; i < words.Length; i++) {
                float prevWordTS = i == 0 ? 0 : getTimestampForWord(words[i - 1]);
                float currWordTS = getTimestampForWord(words[i]);
                float wordDuration = (currWordTS - prevWordTS) * 1000;
                float charDuration = wordDuration / (i == words.Length - 1 ? words[i].Length + 1 : words[i].Length);
                foreach(char ch in words[i]) {
                    characterLengths.Add(new Tuple<string, float>(ch.ToString(), charDuration));
                }
                if(i == words.Length - 1) {
                    characterLengths.Add(new Tuple<string, float>("NONE", charDuration));
                }
            }
            prevChar = "NONE";
            currChar = characterLengths[0].Item1;
        }

        public void Update() {
            if(!speak) {
                return;
            }
            long currentTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            long timeElapsed = currentTime - currentReferenceTS;
            Tuple<string, float> characterLength = characterLengths[currentCharIndex];
            float ratio = timeElapsed / characterLength.Item2;
            //Debug.Log(String.Format("Between {0} and {1}: {2}", prevChar, currChar, ratio));
            setBlendShapeWeights(ratio);
            if(timeElapsed > characterLength.Item2) {
                nextChar(timeElapsed - characterLength.Item2);
                //Debug.Log("---NEXT---");
                return;
            }
        }

        private void nextChar(float overlap) {
            prevChar = characterLengths[currentCharIndex].Item1;
            currentCharIndex++;
            currentReferenceTS = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if(currentCharIndex == characterLengths.Count) {
                speak = false;
                Debug.Log(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - startSentenceTS);
                return;
            }
            currChar = characterLengths[currentCharIndex].Item1;
            float length = characterLengths[currentCharIndex].Item2;
            characterLengths[currentCharIndex] = new Tuple<string, float>(currChar, length - overlap);
        }

        private void setBlendShapeWeights(float ratio) {
            List<BlendShapeInfo> blendShapesForFirstLetter = DataManager.getAllBlendWeightsForLetter(prevChar);
            List<BlendShapeInfo> blendShapesForSecondLetter = DataManager.getAllBlendWeightsForLetter(currChar);
            for(int i = 0; i < blendShapesForFirstLetter.Count; i++) {
                float weight = interpolate(blendShapesForFirstLetter[i].weight, blendShapesForSecondLetter[i].weight, ratio); 
                animationManager.setBlendShapeWeight(blendShapesForFirstLetter[i].name, weight);
            }
        }

        private float interpolate(float val1, float val2, float ratio) {
            return val1 + (val2 - val1) * timeFunction(ratio);
        }

        private float timeFunction(float t) {
            double power = 2;
            return (float)((1 - Math.Pow(Math.Exp(-t), power)) / (1 - Math.Pow(Math.Exp(-1), power)));
        }

        private float getTimestampForWord(string word) {
            float timestamp;
            bool foundTimestamp = timestampsForWords.TryGetValue(word, out timestamp);
            if(!foundTimestamp) {
                Debug.Log("Could not find timestamp for word " + word);
            }
            return timestamp;
        }
    }
}
