using UnityEngine;
using System.Collections;

public class SoundMaker : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}


	void OnCollisionEnter(Collision col) {
		
		if (col.gameObject.tag == "enemy") {
			Debug.Log ("yes");
			GetComponent<AudioSource>().Play ();


			
		}
		
	}


	
}
