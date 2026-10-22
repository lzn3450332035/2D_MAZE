using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Load : MonoBehaviour
{
    public static int index;
    public void LoadTable()
    {
        
        
        index = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(1);
    }
}
