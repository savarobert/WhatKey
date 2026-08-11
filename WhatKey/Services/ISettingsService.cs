using WhatKey.Models;

namespace WhatKey.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
