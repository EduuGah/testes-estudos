package br.unip.mobile.aula7;

import android.graphics.Color;
import android.os.Bundle;
import android.util.Patterns;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

/**
 * Valida os campos do login antes de qualquer autenticação:
 * 1) campos vazios, 2) formato de e-mail, 3) tamanho mínimo da senha.
 */
public class MainActivity extends AppCompatActivity {

    private static final int TAMANHO_MINIMO_SENHA = 6;

    private EditText emailEditText, senhaEditText;
    private TextView mensagemTextView;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        emailEditText = findViewById(R.id.emailEditText);
        senhaEditText = findViewById(R.id.senhaEditText);
        mensagemTextView = findViewById(R.id.mensagemTextView);
        Button entrarButton = findViewById(R.id.entrarButton);

        entrarButton.setOnClickListener(v -> validar());
    }

    private void validar() {
        // trim() remove espaços: "   " também conta como vazio
        String email = emailEditText.getText().toString().trim();
        String senha = senhaEditText.getText().toString();

        // 1) Campo vazio
        if (email.isEmpty() || senha.isEmpty()) {
            if (email.isEmpty()) emailEditText.setError("Informe o e-mail");
            if (senha.isEmpty()) senhaEditText.setError("Informe a senha");
            erro("Preencha todos os campos!");
            return;
        }

        // 2) E-mail inválido: padrão de e-mail pronto do Android
        if (!Patterns.EMAIL_ADDRESS.matcher(email).matches()) {
            emailEditText.setError("Formato inválido");
            erro("Digite um e-mail válido!");
            return;
        }

        // 3) Senha curta
        if (senha.length() < TAMANHO_MINIMO_SENHA) {
            senhaEditText.setError("Mínimo de " + TAMANHO_MINIMO_SENHA + " caracteres");
            erro("A senha deve ter no mínimo " + TAMANHO_MINIMO_SENHA + " caracteres!");
            return;
        }

        // Dados aprovados: só aqui seriam enviados ao servidor de autenticação (por HTTPS).
        // A senha nunca é exibida, registrada em log ou guardada em texto puro no aparelho.
        senhaEditText.setText("");
        mensagemTextView.setTextColor(Color.parseColor("#067647"));
        mensagemTextView.setBackgroundColor(Color.parseColor("#DCFAE6"));
        mensagemTextView.setText("Login validado com segurança!");
    }

    private void erro(String mensagem) {
        mensagemTextView.setTextColor(Color.parseColor("#B42318"));
        mensagemTextView.setBackgroundColor(Color.parseColor("#FEE4E2"));
        mensagemTextView.setText(mensagem);
    }
}
