using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class DataManager {
    private static Dictionary<String, List<BlendShapeInfo>> weightsForLetter = new Dictionary<string, List<BlendShapeInfo>>();
    private static string[] allBlendShapes;

    public static void loadBlendShapeWeights() {
        TextAsset stringData = Resources.Load<TextAsset>("blendShapeWeightsForLetters");
        BlendWeightsData blendWeightsData = JsonUtility.FromJson<BlendWeightsData>(stringData.ToString());
        allBlendShapes = blendWeightsData.allBlendShapes;

        foreach(BlendWeightsForLetter blendWeightsForLetter in blendWeightsData.data) {
            List<BlendShapeInfo> finalList = new List<BlendShapeInfo>();
            List<string> blendShapesInListForLetter = new List<string>();

            foreach(BlendShapeInfo blendShapeInfo in blendWeightsForLetter.shapeData) {
                finalList.Add(blendShapeInfo);
                blendShapesInListForLetter.Add(blendShapeInfo.name);
            }

            foreach(string blendShapeName in allBlendShapes) {
                if(!blendShapesInListForLetter.Contains(blendShapeName)) {
                    finalList.Add(new BlendShapeInfo(blendShapeName, 0));
                }
            }
            finalList = finalList.OrderBy(obj => obj.name).ToList();

            foreach(string letter in blendWeightsForLetter.letters) {
                weightsForLetter.Add(letter, finalList);
            }
        }
        List<BlendShapeInfo> blendShapesForNone = new List<BlendShapeInfo>();
        foreach(string blendShapeName in allBlendShapes) {
            blendShapesForNone.Add(new BlendShapeInfo(blendShapeName, 0));
        }
        weightsForLetter.Add("NONE", blendShapesForNone);
    }

    public static List<BlendShapeInfo> getAllBlendWeightsForLetter(string letter) {
        List<BlendShapeInfo> shapeData;
        if(!weightsForLetter.TryGetValue(letter, out shapeData)) {
            Debug.LogError("No blend shape data for letter " + letter);
            return null;
        }
        return shapeData;
    }
}

[System.Serializable]
class BlendWeightsData {
    public string[] allBlendShapes = null;
    public BlendWeightsForLetter[] data = null;
}

[System.Serializable]
class BlendWeightsForLetter {
    public string[] letters = null;
    public BlendShapeInfo[] shapeData = null;
}

[System.Serializable]
public class BlendShapeInfo {
    public string name = "";
    public float weight = 0;

    public BlendShapeInfo(string name, float weight) {
        this.name = name;
        this.weight = weight;
    }

    public string format() {
        return String.Format("{0}: {1}", name, weight);
    }
}
