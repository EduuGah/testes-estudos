package br.unip.mobile.aula6;

import android.content.SharedPreferences;
import android.os.Bundle;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

/**
 * Salva o nome do usuário com SharedPreferences e o recupera
 * sempre que o aplicativo é aberto novamente.
 */
public class MainActivity extends AppCompatActivity {

    private static final String ARQUIVO = "preferencias_usuario";
    private static final String CHAVE_NOME = "nome";

    private SharedPreferences preferencias;
    private TextView saudacaoTextView, infoTextView;
    private EditText nomeEditText;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        saudacaoTextView = findViewById(R.id.saudacaoTextView);
        infoTextView = findViewById(R.id.infoTextView);
        nomeEditText = findViewById(R.id.nomeEditText);
        Button salvarButton = findViewById(R.id.salvarButton);
        Button limparButton = findViewById(R.id.limparButton);

        // MODE_PRIVATE: só este aplicativo consegue ler o arquivo
        preferencias = getSharedPreferences(ARQUIVO, MODE_PRIVATE);

        // Recuperação: ao abrir o app, lê o nome salvo anteriormente
        mostrarNomeSalvo();

        salvarButton.setOnClickListener(v -> salvarNome());
        limparButton.setOnClickListener(v -> {
            preferencias.edit().remove(CHAVE_NOME).apply();
            mostrarNomeSalvo();
            Toast.makeText(this, "Nome apagado.", Toast.LENGTH_SHORT).show();
        });
    }

    private void salvarNome() {
        String nome = nomeEditText.getText().toString().trim();
        if (nome.isEmpty()) {
            nomeEditText.setError("Digite um nome antes de salvar");
            return;
        }
        // Salvamento: o Editor grava o par chave/valor e apply() persiste em segundo plano
        preferencias.edit().putString(CHAVE_NOME, nome).apply();
        nomeEditText.setText("");
        mostrarNomeSalvo();
        Toast.makeText(this, "Nome salvo com sucesso!", Toast.LENGTH_SHORT).show();
    }

    private void mostrarNomeSalvo() {
        String nome = preferencias.getString(CHAVE_NOME, null);   // null = valor padrão se não existir
        if (nome == null) {
            saudacaoTextView.setText("Olá! Qual é o seu nome?");
            infoTextView.setText("Nenhum nome salvo neste aparelho.");
        } else {
            saudacaoTextView.setText("Bem-vindo de volta, " + nome + "!");
            infoTextView.setText("Nome recuperado do SharedPreferences.");
        }
    }
}
