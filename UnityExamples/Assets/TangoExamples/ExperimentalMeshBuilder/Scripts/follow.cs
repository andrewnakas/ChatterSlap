using UnityEngine;
using System.Collections;

public class follow : MonoBehaviour {
	public GameObject tangoCam;
	public Vector3 dirpos; 
	public bool killSwitch;
	public int counter;
	// Use this for initialization
	void Start () {
		tangoCam = GameObject.Find ("Camera");
	}
	
	// Update is called once per frame
	void Update () {

		if (killSwitch == true) {

			counter++;

		} else {


			transform.LookAt (tangoCam.transform.position);
		}
		if (counter > 100) {

			Destroy (gameObject);

		}
	dirpos =	tangoCam.transform.position - transform.position;

		dirpos.Normalize ();

		//transform.Translate (dirpos* 5 * Time.deltaTime);
	
	}

	void FixedUpdate() {

		if (killSwitch == false) {
			GetComponent<Rigidbody> ().MovePosition (transform.position + dirpos * Time.deltaTime);
		}
	}

	void OnCollisionEnter(Collision collision) {
		if (collision.gameObject.tag == "ball") {

			gameObject.layer = 1 ;

			killSwitch = true;

		}
		}


}
