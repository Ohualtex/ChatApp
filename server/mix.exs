defmodule ChatApp.MixProject do
  use Mix.Project

  def project, do: [app: :chat_app, version: "0.1.0", elixir: "~> 1.19"]
  def application, do: [mod: {ChatApp, []}, extra_applications: [:logger]]
end
