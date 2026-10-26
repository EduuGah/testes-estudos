package br.unip.mobile.aula4;

import android.os.Bundle;
import android.widget.Button;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;

/** Tela de menu: cada botão leva a uma funcionalidade do sistema. */
public class MainActivity extends AppCompatActivity {

    private TextView statusTextView;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        statusTextView = findViewById(R.id.statusTextView);
        Button cadastroButton = findViewById(R.id.cadastroButton);
        Button consultarButton = findViewById(R.id.consultarButton);
        Button configuracoesButton = findViewById(R.id.configuracoesButton);
        Button sairButton = findViewById(R.id.sairButton);

        // Um único método trata as três opções de navegação
        cadastroButton.setOnClickListener(v -> abrirOpcao("Cadastro"));
        consultarButton.setOnClickListener(v -> abrirOpcao("Consultar"));
        configuracoesButton.setOnClickListener(v -> abrirOpcao("Configurações"));

        // Sair pede confirmação antes de fechar o aplicativo
        sairButton.setOnClickListener(v -> new AlertDialog.Builder(this)
                .setTitle("Sair")
                .setMessage("Deseja realmente sair do aplicativo?")
                .setPositiveButton("Sim", (dialogo, qual) -> finish())
                .setNegativeButton("Cancelar", null)
                .show());
    }

    private void abrirOpcao(String opcao) {
        statusTextView.setText("Opção selecionada: " + opcao);
        Toast.makeText(this, "Abrindo " + opcao + "...", Toast.LENGTH_SHORT).show();
    }
}
