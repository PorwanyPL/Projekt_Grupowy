import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import '../config/api_config.dart';
import 'api_exception.dart';

/// Cienka warstwa HTTP: JSON w obie strony i jednolita obsługa błędów
/// (ProblemDetails / ValidationProblemDetails z ASP.NET).
class ApiClient {
  ApiClient({http.Client? client, String? baseUrl})
      : _client = client ?? http.Client(),
        _baseUrl = baseUrl ?? ApiConfig.baseUrl;

  final http.Client _client;
  final String _baseUrl;

  String? _token;

  /// Wywoływane, gdy serwer odpowie 401 na żądanie wysłane z tokenem (sesja wygasła / dostęp zablokowany).
  void Function()? onUnauthorized;

  /// Ustawia token dołączany do każdego żądania (null = wylogowany).
  void setToken(String? token) => _token = token;

  Map<String, String> get _headers => {
        'Content-Type': 'application/json; charset=utf-8',
        if (_token != null) 'Authorization': 'Bearer $_token',
      };

  Uri _uri(String path, [Map<String, String>? query]) =>
      Uri.parse('$_baseUrl$path').replace(queryParameters: query);

  Future<dynamic> get(String path, {Map<String, String>? query}) =>
      _send(() => _client.get(_uri(path, query), headers: _headers));

  Future<dynamic> post(String path, [Object? body]) => _send(() => _client.post(
      _uri(path),
      headers: _headers,
      body: body == null ? null : jsonEncode(body)));

  Future<dynamic> put(String path, Object body) => _send(
      () => _client.put(_uri(path), headers: _headers, body: jsonEncode(body)));

  Future<dynamic> delete(String path) =>
      _send(() => _client.delete(_uri(path), headers: _headers));

  Future<dynamic> _send(Future<http.Response> Function() request) async {
    final http.Response response;
    try {
      response = await request().timeout(ApiConfig.timeout);
    } on TimeoutException {
      throw const ApiException(
          'Serwer nie odpowiada. Sprawdź, czy backend jest uruchomiony.');
    } on http.ClientException {
      throw ApiException(
          'Brak połączenia z serwerem ($_baseUrl). Sprawdź adres API i czy backend działa.');
    }

    final bodyText = utf8.decode(response.bodyBytes);

    // 401 przy aktywnej sesji = token nieważny; przy logowaniu (brak tokenu) to zwykły błąd danych.
    if (response.statusCode == 401 && _token != null) {
      onUnauthorized?.call();
      throw const ApiException(
          'Sesja wygasła lub dostęp został zablokowany. Zaloguj się ponownie.',
          statusCode: 401);
    }

    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (bodyText.isEmpty) return null;
      return jsonDecode(bodyText);
    }
    throw ApiException(_errorMessage(response.statusCode, bodyText),
        statusCode: response.statusCode);
  }

  String _errorMessage(int status, String bodyText) {
    try {
      final json = jsonDecode(bodyText);
      if (json is Map<String, dynamic>) {
        final errors = json['errors'];
        if (errors is Map<String, dynamic> && errors.isNotEmpty) {
          return errors.values
              .expand((v) => v is List ? v : [v])
              .map((v) => v.toString())
              .join('\n');
        }
        final detail = json['detail'];
        if (detail is String && detail.isNotEmpty) return detail;
        final title = json['title'];
        if (title is String && title.isNotEmpty) return title;
      }
    } catch (_) {
      // odpowiedź nie jest JSON-em - użyj komunikatu ogólnego
    }
    if (status == 401) return 'Nieprawidłowy adres e-mail lub hasło.';
    return 'Błąd serwera (HTTP $status).';
  }
}
