package br.unip.mobile.aula1;

import android.os.Bundle;

import androidx.appcompat.app.AppCompatActivity;

/**
 * Activity principal criada pelo modelo "Empty Views Activity".
 * Ela apenas carrega o layout activity_main.xml, onde o texto foi alterado.
 */
public class MainActivity extends AppCompatActivity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
    }
}
