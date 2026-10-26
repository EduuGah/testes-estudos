import 'package:flutter/material.dart';

void main() => runApp(const VerificadorApp());

class VerificadorApp extends StatelessWidget {
  const VerificadorApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Verificador de Senha',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(colorSchemeSeed: const Color(0xFF1F4E79), useMaterial3: true),
      home: const TelaVerificador(),
    );
  }
}

/// Resultado da análise: texto, cor e nível (0 a 1) para a barra
class Forca {
  final String rotulo;
  final Color cor;
  final double nivel;
  const Forca(this.rotulo, this.cor, this.nivel);
}

/// Regra de classificação pelo tamanho da senha:
///   menos de 6 caracteres  -> Fraca
///   de 6 a 9 caracteres    -> Média
///   10 ou mais caracteres  -> Forte
Forca? analisarSenha(String senha) {
  if (senha.isEmpty) return null;
  if (senha.length < 6) return const Forca('Senha Fraca', Colors.red, 0.33);
  if (senha.length < 10) return const Forca('Senha Média', Colors.orange, 0.66);
  return const Forca('Senha Forte', Colors.green, 1.0);
}

class TelaVerificador extends StatefulWidget {
  const TelaVerificador({super.key});

  @override
  State<TelaVerificador> createState() => _TelaVerificadorState();
}

class _TelaVerificadorState extends State<TelaVerificador> {
  final senhaController = TextEditingController();
  bool ocultar = true;
  Forca? forca;

  @override
  Widget build(BuildContext context) {
    final tamanho = senhaController.text.length;
    return Scaffold(
      backgroundColor: Colors.blue[50],
      appBar: AppBar(
        title: const Text('Segurança Mobile'),
        backgroundColor: const Color(0xFF1F4E79),
        foregroundColor: Colors.white,
      ),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 420),
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text('Verificador de Senha',
                    textAlign: TextAlign.center,
                    style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold)),
                const SizedBox(height: 24),
                TextField(
                  controller: senhaController,
                  obscureText: ocultar,
                  // onChanged: a análise acontece a cada caractere digitado
                  onChanged: (valor) => setState(() => forca = analisarSenha(valor)),
                  decoration: InputDecoration(
                    labelText: 'Digite sua senha',
                    filled: true,
                    fillColor: Colors.white,
                    border: const OutlineInputBorder(),
                    suffixIcon: IconButton(
                      icon: Icon(ocultar ? Icons.visibility : Icons.visibility_off),
                      onPressed: () => setState(() => ocultar = !ocultar),
                    ),
                  ),
                ),
                const SizedBox(height: 20),
                // Barra visual da força da senha
                LinearProgressIndicator(
                  value: forca?.nivel ?? 0,
                  minHeight: 10,
                  color: forca?.cor,
                  backgroundColor: Colors.grey[300],
                ),
                const SizedBox(height: 16),
                Text(
                  forca?.rotulo ?? 'Digite uma senha',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                      fontSize: 22, fontWeight: FontWeight.bold, color: forca?.cor ?? Colors.grey[700]),
                ),
                const SizedBox(height: 8),
                Text('$tamanho caractere(s) – Fraca < 6 · Média 6 a 9 · Forte 10 ou mais',
                    textAlign: TextAlign.center, style: TextStyle(color: Colors.grey[700])),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
