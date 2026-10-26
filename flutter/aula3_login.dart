import 'package:flutter/material.dart';

// Ponto de entrada: runApp() inicia o aplicativo com o widget raiz
void main() => runApp(const LoginApp());

/// Widget raiz: configura o MaterialApp (tema e tela inicial)
class LoginApp extends StatelessWidget {
  const LoginApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Login Flutter',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(colorSchemeSeed: const Color(0xFF1F4E79), useMaterial3: true),
      home: const TelaLogin(),
    );
  }
}

/// Tela com estado: guarda a mensagem exibida após o login
class TelaLogin extends StatefulWidget {
  const TelaLogin({super.key});

  @override
  State<TelaLogin> createState() => _TelaLoginState();
}

class _TelaLoginState extends State<TelaLogin> {
  // Controllers leem o texto digitado nos campos
  final emailController = TextEditingController();
  final senhaController = TextEditingController();
  String mensagem = '';

  void fazerLogin() {
    final email = emailController.text.trim();
    // setState redesenha a tela com a nova mensagem
    setState(() {
      mensagem = email.isEmpty || senhaController.text.isEmpty
          ? 'Preencha e-mail e senha.'
          : 'Bem-vindo(a), $email!';
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Portal Acadêmico'),
        backgroundColor: const Color(0xFF1F4E79),
        foregroundColor: Colors.white,
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(32),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 400),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.school, size: 64, color: Color(0xFF1F4E79)),
                const SizedBox(height: 12),
                const Text('Entre na sua conta',
                    style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold)),
                const SizedBox(height: 24),
                TextField(
                  controller: emailController,
                  keyboardType: TextInputType.emailAddress,
                  decoration: const InputDecoration(
                    labelText: 'E-mail',
                    prefixIcon: Icon(Icons.email_outlined),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 16),
                TextField(
                  controller: senhaController,
                  obscureText: true, // esconde os caracteres da senha
                  decoration: const InputDecoration(
                    labelText: 'Senha',
                    prefixIcon: Icon(Icons.lock_outline),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 24),
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: ElevatedButton(
                    onPressed: fazerLogin,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF1F4E79),
                      foregroundColor: Colors.white,
                    ),
                    child: const Text('Login', style: TextStyle(fontSize: 16)),
                  ),
                ),
                const SizedBox(height: 24),
                // Mensagem de boas-vindas
                Text(mensagem,
                    textAlign: TextAlign.center,
                    style: const TextStyle(fontSize: 18, color: Color(0xFF067647))),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
