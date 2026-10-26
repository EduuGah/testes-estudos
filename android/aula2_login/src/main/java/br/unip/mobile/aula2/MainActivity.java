package br.unip.mobile.aula2;

import android.os.Bundle;
import android.widget.Button;
import android.widget.EditText;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

/**
 * A interface fica toda no XML; o Java só conecta os componentes
 * e responde ao clique do botão (separação entre interface e lógica).
 */
public class MainActivity extends AppCompatActivity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        EditText emailEditText = findViewById(R.id.emailEditText);
        Button entrarButton = findViewById(R.id.entrarButton);

        entrarButton.setOnClickListener(v -> {
            String email = emailEditText.getText().toString().trim();
            Toast.makeText(this, "Bem-vindo(a), " + (email.isEmpty() ? "visitante" : email) + "!", Toast.LENGTH_LONG).show();
        });
    }
}
