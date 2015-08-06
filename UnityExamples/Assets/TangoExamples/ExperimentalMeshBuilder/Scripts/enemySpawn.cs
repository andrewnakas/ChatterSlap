using UnityEngine;
using System.Collections;

public class enemySpawn : MonoBehaviour {
	public int roll;
	public GameObject enemy;
public GameObject cam;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		roll = Random.Range (0, 300);


		if (roll == 1) {

			Instantiate (enemy, transform.position, Quaternion.Inverse(cam.transform.rotation));

		}
	}
}
