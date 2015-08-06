using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class trigger : MonoBehaviour {
	public float health; 
	public GameObject playagain; 
	public GameObject[] gameObjects;
	public GameObject healthText;

	Text text; 
		// Use this for initialization
	void Start () {
		health = 100;
		text = healthText.GetComponent<Text> ();
	}
	
	// Update is called once per frame
	void Update () {
	
		text.text = "Health: " + health;
		if (health <0 )

		{
	
			Time.timeScale = 0; 
			playagain.SetActive(true);




		
		}
		Debug.Log (health);

	
}

	void OnTriggerStay(Collider col) {
		
		if (col.gameObject.tag == "enemy") {
			
			health --;
			Debug.Log("you lose");
			
		}
		
	}

	public void reset (){


		health = 100;
		Time.timeScale = 1;

		playagain.SetActive(false);


		gameObjects = GameObject.FindGameObjectsWithTag ("enemy");
		
		for(var i = 0 ; i < gameObjects.Length ; i ++)
		{
			Destroy(gameObjects[i]);
		}
		
	}
	
}	
