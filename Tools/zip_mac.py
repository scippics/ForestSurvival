# Упаковывает мак-сборку в архив. Запускать из корня проекта после BuildScript.BuildAll.
import os, zipfile

root = 'Build/macOS'
app = os.path.join(root, 'ForestSurvival.app')
out = os.path.join(root, 'ForestSurvival_macOS.zip')

with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
    for folder, _, files in os.walk(app):
        for name in files:
            path = os.path.join(folder, name)
            arc = os.path.relpath(path, root).replace(os.sep, '/')
            info = zipfile.ZipInfo.from_file(path, arc)
            info.compress_type = zipfile.ZIP_DEFLATED
            # Исполняемый признак сразу в архиве: шаг с chmod становится необязательным.
            mode = 0o755 if arc.endswith('/Contents/MacOS/ForestSurvival') else 0o644
            info.external_attr = (0o100000 | mode) << 16
            with open(path, 'rb') as f:
                z.writestr(info, f.read())
print(out, os.path.getsize(out))
