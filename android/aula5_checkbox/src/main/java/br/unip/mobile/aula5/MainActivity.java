package br.unip.mobile.aula5;

import android.os.Bundle;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import java.util.ArrayList;
import java.util.List;

/** Captura as tecnologias marcadas pelo usuário e mostra um resumo. */
public class MainActivity extends AppCompatActivity {

    private CheckBox javaCheckBox, kotlinCheckBox, flutterCheckBox, pythonCheckBox;
    private TextView resultadoTextView;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        // Liga as variáveis Java aos componentes do XML pelo id
        javaCheckBox = findViewById(R.id.javaCheckBox);
        kotlinCheckBox = findViewById(R.id.kotlinCheckBox);
        flutterCheckBox = findViewById(R.id.flutterCheckBox);
        pythonCheckBox = findViewById(R.id.pythonCheckBox);
        resultadoTextView = findViewById(R.id.resultadoTextView);
        Button verificarButton = findViewById(R.id.verificarButton);

        verificarButton.setOnClickListener(v -> verificarResposta());
    }

    private void verificarResposta() {
        List<String> selecionadas = new ArrayList<>();
        CheckBox[] opcoes = {javaCheckBox, kotlinCheckBox, flutterCheckBox, pythonCheckBox};

        // isChecked() devolve true quando a caixa está marcada
        for (CheckBox opcao : opcoes) {
            if (opcao.isChecked()) {
                selecionadas.add(opcao.getText().toString());
            }
        }

        if (selecionadas.isEmpty()) {
            resultadoTextView.setText("Nenhuma opção marcada. Selecione pelo menos uma tecnologia.");
            return;
        }

        boolean mobile = kotlinCheckBox.isChecked() || flutterCheckBox.isChecked();
        String resposta = "Você conhece " + selecionadas.size() + " tecnologia(s): "
                + String.join(", ", selecionadas) + ".\n\n"
                + (mobile ? "Ótimo! Kotlin e Flutter são muito usados no desenvolvimento mobile."
                          : "Dica: Kotlin e Flutter são as tecnologias mais usadas para apps mobile.");
        resultadoTextView.setText(resposta);
    }
}
